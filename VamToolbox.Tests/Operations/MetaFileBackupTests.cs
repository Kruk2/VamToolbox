using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Text;
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
    public async Task Backup_WhenBackupAlreadyExist_ShouldOverrideIt()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Backup();

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
        using var memoryStream = new MemoryStream(file.Contents);
        using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read, true);
        string? metaFile = null;
        string? backupFile = null;

        {
            var entry = zipArchive.GetEntry("meta.json");
            if (entry != null) {
                using var stream = entry.Open();
                using var readStream = new StreamReader(stream, Encoding.UTF8);
                metaFile = readStream.ReadToEnd();
            }
        }

        {
            var entry = zipArchive.GetEntry("meta.json.toolboxbak");
            if (entry != null) {
                using var stream = entry.Open();
                using var readStream = new StreamReader(stream, Encoding.UTF8);
                backupFile = readStream.ReadToEnd();
            }
        }


        return (metaFile, backupFile);
    }

    private static MockFileData CreateZipFile(string? metaFile = null, string? backupFile = null)
    {
        using var memoryStream = new MemoryStream();
        {
            using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true);
            if (metaFile is not null) {
                var entry = zipArchive.CreateEntry("meta.json");
                using var stream = entry.Open();
                using var writeStream = new StreamWriter(stream, Encoding.UTF8);
                writeStream.Write(metaFile);
            }

            if (backupFile is not null) {
                var entry = zipArchive.CreateEntry("meta.json.toolboxbak");
                using var stream = entry.Open();
                using var writeStream = new StreamWriter(stream, Encoding.UTF8);
                writeStream.Write(backupFile);
            }
        }

        return new MockFileData(memoryStream.ToArray());
    }

    private Task Backup(bool dryRun = false) => _backuper.Backup(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    });

    private Task Restore(bool dryRun = false) => _backuper.Restore(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    });
}
