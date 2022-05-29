using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using VamToolbox.Helpers;
using VamToolbox.Operations.Destructive;
using Xunit;

namespace VamToolbox.Tests.Operations;

public class RemoveSoftLinksAndEmptyDirsTests
{
    private const string VamDir = "C:/VAM/";

    private readonly CustomFixture _fixture;
    private readonly ISoftLinker _softLinker;
    private readonly MockFileSystem _fs;
    private readonly RemoveSoftLinksAndEmptyDirs _operation;

    public RemoveSoftLinksAndEmptyDirsTests()
    {
        _fixture = new CustomFixture();
        _softLinker = _fixture.Freeze<ISoftLinker>();
        _fs = _fixture.Freeze<MockFileSystem>();
        _operation = _fixture.Create<RemoveSoftLinksAndEmptyDirs>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Execute_WhenHasSoftLinkedFiles_RemoveThem(bool dryRun)
    {
        var softFileInAddonPackages = VamDir + KnownNames.AddonPackages + "/softLink.var";
        var notSoftFileInAddonPackages = VamDir + KnownNames.AddonPackages + "/notSoftLink.var";
        var softFileInMorphDir = VamDir + KnownNames.FemaleMorphsDir + "/softLink.var";
        var notSoftFileInMorphDir = VamDir + KnownNames.MaleMorphsDir + "/notSoftLink.var";
        var softFileInHairDir = VamDir + KnownNames.HairPresetsDir + "/softLink.var";
        var notSoftFileInHairDir = VamDir + KnownNames.MaleClothDir + "/notSoftLink.var";

        AddFile(softFileInAddonPackages);
        AddFile(notSoftFileInAddonPackages, softLink: false);
        AddFile(softFileInMorphDir);
        AddFile(notSoftFileInMorphDir, softLink: false);
        AddFile(softFileInHairDir);
        AddFile(notSoftFileInHairDir, softLink: false);

        await Execute(dryRun);

        using var _ = new AssertionScope();
        _fs.FileExists(softFileInAddonPackages).Should().Be(dryRun);
        _fs.FileExists(notSoftFileInAddonPackages).Should().BeTrue();
        _fs.FileExists(softFileInMorphDir).Should().Be(dryRun);
        _fs.FileExists(notSoftFileInMorphDir).Should().BeTrue();
        _fs.FileExists(softFileInHairDir).Should().Be(dryRun);
        _fs.FileExists(notSoftFileInHairDir).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Execute_WhenHasEmptyDirs_RemoveThem(bool dryRun)
    {
        var notEmptyDirButWithSoftLink = VamDir + KnownNames.AddonPackages + "/notEmptyDirWithSoftLink";
        var softFileInNonEmptyDir = notEmptyDirButWithSoftLink + "/softLink.var";
        AddFile(softFileInNonEmptyDir);

        var notEmptyDir = VamDir + KnownNames.AddonPackages + "/notEmptyDir";
        var fileInNonEmptyDir = notEmptyDir + "/softLink.var";
        AddFile(fileInNonEmptyDir, softLink: false);

        var emptyDirInAddonPackages = VamDir + KnownNames.AddonPackages + "/emptyDir";
        AddDir(emptyDirInAddonPackages);

        var emptyDirInHairs = VamDir + KnownNames.HairPresetsDir + "/emptyDir";
        var nestedEmptyDirInHairs = emptyDirInHairs + "/nestedDir";
        AddDir(emptyDirInHairs);
        AddDir(nestedEmptyDirInHairs);

        await Execute(dryRun);

        using var _ = new AssertionScope();
        _fs.Directory.Exists(notEmptyDirButWithSoftLink).Should().Be(dryRun);
        _fs.FileExists(notEmptyDir).Should().BeTrue();
        _fs.Directory.Exists(emptyDirInAddonPackages).Should().Be(dryRun);
        _fs.Directory.Exists(nestedEmptyDirInHairs).Should().Be(dryRun);
        _fs.Directory.Exists(emptyDirInHairs).Should().Be(dryRun);
    }

    private void AddDir(string path) => _fs.AddDirectory(path);
    private void AddFile(string path, bool softLink = true)
    {
        path = _fs.Path.GetFullPath(path);
        _fs.AddFile(path, string.Empty);
        _softLinker.IsSoftLink(path).Returns(softLink);
    }

    private Task Execute(bool dryRun = false)
    {
        return _operation.ExecuteAsync(new() { DryRun = dryRun, VamDir = VamDir, Threads = 1 });
    }
}
