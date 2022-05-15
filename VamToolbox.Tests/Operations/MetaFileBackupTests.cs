using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Backups;
using Xunit;

namespace VamToolbox.Tests.Operations;
public class MetaFileBackupTests 
{
    private readonly MockFileSystem _fs;
    private readonly MetaFileBackuper _backuper;
    private const string AddondsDir = "C:/VaM/AddonPackages/";

    public MetaFileBackupTests()
    {
        _fs = new MockFileSystem(new Dictionary<string, MockFileData> {
            [AddondsDir + "a.var"] = CreateZipFile(metaFile: "test-meta-content")
        });
        var fixture = new CustomFixture();
        fixture.AddFileSystem(_fs);
        _backuper = fixture.Create<MetaFileBackuper>();
    }

    [Fact]
    public async Task Backup_WhenCorruptedZipFile_ShouldNotCrash()
    {
        _fs.AddFile(AddondsDir + "a.var", "corrupted-zip-data");
        await Backup();

        var file = _fs.GetFile(AddondsDir + "a.var");
        file.TextContents.Should().Be("corrupted-zip-data");
    }

    [Fact]
    public async Task Backup_WhenInNestedDir_ShouldBackupMetaFile()
    {
        _fs.AddFile(AddondsDir + "test/a.var", CreateZipFile(metaFile: "test-meta-content"));
        await Backup();

        var file = _fs.GetFile(AddondsDir + "test/a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile!.Should().Be("test-meta-content");
    }

    [Fact]
    public async Task Backup_ShouldBackupMetaFile()
    {
        await Backup();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile!.Should().Be("test-meta-content");
    }

    [Fact]
    public async Task Backup_WhenMetaFileIsMissing_ShouldSkipIt()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile( backupFile: "backup-content"));
        await Backup();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile.Should().BeNull();
        backupFile!.Should().Be("backup-content");
    }

    [Fact]
    public async Task Backup_WhenDryRun_ShouldNotChangeAnything()
    {
        await Backup(dryRun: true);

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile.Should().BeNull();
    }

    [Fact]
    public async Task Backup_WhenBackupAlreadyExists_ShouldNotChangeAnything()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Backup(overrideBackups: false);

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile.Should().Be("backup-content");
    }

    [Fact]
    public async Task Backup_WhenBackupAlreadyExistAndWeAllowed_ShouldOverrideIt()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Backup(overrideBackups: true);

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile!.Should().Be("test-meta-content");
    }

    [Fact]
    public async Task Restore_ShouldRestoreMetaFile()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Restore();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("backup-content");
        backupFile!.Should().Be("backup-content");
    }

    [Fact]
    public async Task Restore_WhenBackupFileIsMissing_ShouldSkipIt()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content"));
        await Restore();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile.Should().BeNull();
    }

    [Fact]
    public async Task Restore_WhenDryRun_ShouldNotChangeAnything()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Restore(dryRun: true);

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile!.Should().Be("backup-content");
    }

    [Fact]
    public async Task Restore_WhenMetaFileIsMissing_ShouldRestoreMetaFile()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(backupFile: "backup-content"));
        await Restore();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("backup-content");
        backupFile!.Should().Be("backup-content");
    }

    private static (string? metaFile, string? backupFile) ReadZipFile(MockFileData file)
    {
        var files = ZipTestHelpers.ReadZipFile(file);
        files.TryGetValue("meta.json", out var metaFile);
        files.TryGetValue("meta.json.toolboxbak", out var backupFile);

        return (metaFile, backupFile);
    }

    private static MockFileData CreateZipFile(string? metaFile = null, string? backupFile = null)
    {
        var files = new Dictionary<string, string>();
        if (metaFile is not null) {
            files["meta.json"] = metaFile;
        }

        if (backupFile is not null) {
            files["meta.json.toolboxbak"] = backupFile;
        }

        return ZipTestHelpers.CreateZipFile(files);
    }

    private Task Backup(bool dryRun = false, bool overrideBackups = false) => _backuper.Backup(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    }, overrideBackups);

    private Task Restore(bool dryRun = false) => _backuper.Restore(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    });
}
