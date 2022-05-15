using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using NSubstitute;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using Xunit;

namespace VamToolbox.Tests.Helpers;
public class UserPrefsBackuperTests 
{
    private readonly MockFileSystem _fs;
    private readonly UserPrefsBackuper _backuper;
    private const string VamDir = "C:/VaM/";
    private const string PrefsDir = VamDir + "AddonPackagesUserPrefs/";

    public UserPrefsBackuperTests()
    {
        _fs = new MockFileSystem(new Dictionary<string, MockFileData> {
            [PrefsDir + "a.prefs"] = new ("test"),
            [PrefsDir + "b.prefs"] = new ("test2")
        });
        var logger = Substitute.For<ILogger>();
        _backuper = new UserPrefsBackuper(_fs, logger);
    }

    [Fact]
    public void Backup_WhenUserPrefsDirNotExists_ShouldSkip()
    {
        _backuper.Backup(PrefsDir, false);

        _fs.AllFiles.Should().HaveCount(2);
    }

    [Fact]
    public void Backup_ShouldBackupBothFiles()
    {
        _backuper.Backup(VamDir, false);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents);
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents);
    }


    [Fact]
    public void Backup_WhenDryRun_ShouldSkipBackup()
    {
        _backuper.Backup(VamDir, true);

        _fs.AllFiles.Should().HaveCount(2);
        _fs.FileExists(PrefsDir + "a.prefs");
        _fs.FileExists(PrefsDir + "b.prefs");
    }

    [Fact]
    public void Backup_WhenBackupExitst_ShouldOverrideIt()
    {
        _fs.AddFile(PrefsDir + "a.prefs.toolboxbak", new MockFileData("old_backup"));
        _backuper.Backup(VamDir, false);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents);
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents);
    }

    [Fact]
    public void Restore_WhenUserPrefsDirNotExists_ShouldSkip()
    {
        _backuper.Restore(PrefsDir, false);

        _fs.AllFiles.Should().HaveCount(2);
    }

    [Fact]
    public void Restore_ShouldRestoreBothFiles()
    {
        _fs.AddFile(PrefsDir + "a.prefs.toolboxbak", new MockFileData("backup"));
        _fs.AddFile(PrefsDir + "b.prefs.toolboxbak", new MockFileData("backup1"));

        _backuper.Restore(VamDir, false);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be("backup");
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be("backup1");
        _fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents.Should().Be("backup");
        _fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents.Should().Be("backup1");
    }

    [Fact]
    public void Restore_WhenDryRun_ShouldSkipRestore()
    {
        _fs.AddFile(PrefsDir + "a.prefs.toolboxbak", new MockFileData("backup"));
        _fs.AddFile(PrefsDir + "b.prefs.toolboxbak", new MockFileData("backup1"));

        _backuper.Restore(VamDir, true);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be("test");
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be("test2");
        _fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents.Should().Be("backup");
        _fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents.Should().Be("backup1");
    }
}
