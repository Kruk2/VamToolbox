using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive;
using Xunit;

namespace VamToolbox.Tests.Operations;

public class TrustAllVarsOperationTests
{
    private const string FakeVamDir = "C:/VAM/";
    private const string PrefsDirPath = FakeVamDir + "AddonPackagesUserPrefs";
    private readonly CustomFixture _fixture = new();
    private readonly TrustAllVarsOperation _operation;
    private readonly MockFileSystem _fs;

    public TrustAllVarsOperationTests()
    {
        _fs = _fixture.Create<MockFileSystem>();
        _fs.AddDirectory(FakeVamDir);
        _operation = _fixture.Create<TrustAllVarsOperation>();
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefDirectoryDoesnExist_CreatesDirectory(VarPackage varPackage)
    {
        var context = GetContext();

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        _fs.Directory.Exists(PrefsDirPath).Should().BeTrue();
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefFileDoesntExist_CreatesNew(VarPackage varPackage)
    {
        var context = GetContext();

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        var prefPath = GetPrefPath(varPackage);
        _fs.FileExists(prefPath).Should().BeTrue();
        var content = _fs.GetFile(prefPath).TextContents;
        content.Should().Be("{\r\n  \"pluginsAlwaysEnabled\": \"true\",\r\n  \"pluginsAlwaysDisabled\": \"false\"\r\n}");
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefFileDoesntExist_ButInDryMode_SkipCreation(VarPackage varPackage)
    {
        var context = GetContext(dryRun: true);

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        var prefPath = GetPrefPath(varPackage);
        _fs.FileExists(prefPath).Should().BeFalse();
        _fs.Directory.Exists(PrefsDirPath).Should().BeFalse();
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefFileExistsAndHasPluginsAlwaysEnabledToFalse_ChangesItToTrue(VarPackage varPackage)
    {
        var context = GetContext();
        var prefPath = GetPrefPath(varPackage);
        _fs.AddFile(prefPath, new MockFileData("{\"pluginsAlwaysEnabled\": \"false\", \"pluginsAlwaysDisabled\": \"false\"}"));

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        var content = _fs.GetFile(prefPath).TextContents;
        content.Should().Be("{\r\n  \"pluginsAlwaysEnabled\": \"true\",\r\n  \"pluginsAlwaysDisabled\": \"false\"\r\n}");
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefFilExistsAndHasPluginsAlwaysDisabledSetToTrue_DoesntChangeIt(VarPackage varPackage)
    {
        var context = GetContext();
        var prefPath = GetPrefPath(varPackage);
        var content = "{\"pluginsAlwaysEnabled\": \"false\", \"pluginsAlwaysDisabled\": \"true\"}";
        _fs.AddFile(prefPath, new MockFileData(content));

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        var actualContent = _fs.GetFile(prefPath).TextContents;
        actualContent.Should().Be(content);
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefFilExistsAndHasExtraProperties_DoesntChangeThemWhenUpdatingPluginsSetting(VarPackage varPackage)
    {
        var context = GetContext();
        var prefPath = GetPrefPath(varPackage);
        var content = "{ \"whatever\":\"wtf\", \"pluginsAlwaysEnabled\": \"false\", \"pluginsAlwaysDisabled\": \"false\", \"deep\":{\"wtf\": true} }";
        _fs.AddFile(prefPath, new MockFileData(content));

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        var actualContent = _fs.GetFile(prefPath).TextContents;
        actualContent.Should().Be("{\r\n  \"whatever\": \"wtf\",\r\n  \"pluginsAlwaysEnabled\": \"true\",\r\n  \"pluginsAlwaysDisabled\": \"false\",\r\n  \"deep\": {\r\n    \"wtf\": true\r\n  }\r\n}");
    }

    [Theory, CustomAutoData]
    public async Task Trust_WhenPrefFilExistsAndContainsMalformedData_SkipIt(VarPackage varPackage)
    {
        var context = GetContext();
        var prefPath = GetPrefPath(varPackage);
        var content = "{ whatever\":\"wtf }";
        _fs.AddFile(prefPath, new MockFileData(content));

        await _operation.ExecuteAsync(context, new List<VarPackage> { varPackage });

        var actualContent = _fs.GetFile(prefPath).TextContents;
        actualContent.Should().Be(content);
    }

    private static string GetPrefPath(VarPackage var) => Path.Combine(PrefsDirPath, var.Name.Filename[..^3] + "prefs");
    private static OperationContext GetContext(bool dryRun = false) => new() { DryRun = dryRun, VamDir = FakeVamDir, Threads = 6 };
}
