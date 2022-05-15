using System.IO.Abstractions;
using VamToolbox.Logging;

namespace VamToolbox.Helpers;
public interface IUserPrefsBackuper
{
    public void Backup(string vamDir, bool dryRun);
    public void Restore(string vamDir, bool dryRun);
}

public class UserPrefsBackuper : IUserPrefsBackuper
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private const string BackupExtension = ".toolboxbak";

    public UserPrefsBackuper(IFileSystem fileSystem, ILogger logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public void Backup(string vamDir, bool dryRun)
    {
        var userPrefsDir = UserPrefsDir(vamDir);
        if (!_fileSystem.Directory.Exists(userPrefsDir)) {
            _logger.Log("UserPrefsDir doesn't exist. Exiting");
            return;
        }

        foreach (var file in _fileSystem.Directory.EnumerateFiles(userPrefsDir, "*.prefs")) {
            var fileName = _fileSystem.Path.GetFileName(file) + BackupExtension;
            var backupDestination = _fileSystem.Path.Combine(userPrefsDir, fileName);

            if (!dryRun) {
                _fileSystem.File.Copy(file, backupDestination, true);
            }

            _logger.Log($"Backing up {file} to {backupDestination}");
        }
    }

    public void Restore(string vamDir, bool dryRun)
    {
        var userPrefsDir = UserPrefsDir(vamDir);
        if (!_fileSystem.Directory.Exists(userPrefsDir)) {
            _logger.Log("UserPrefsDir doesn't exist. Exiting");
            return;
        }

        foreach (var file in _fileSystem.Directory.EnumerateFiles(userPrefsDir, "*" + BackupExtension)) {
            var fileName = _fileSystem.Path.GetFileNameWithoutExtension(file);
            var restoreDestination = _fileSystem.Path.Combine(userPrefsDir, fileName);

            if (!dryRun) {
                _fileSystem.File.Copy(file, restoreDestination, true);
            }

            _logger.Log($"Restoring {file} to {restoreDestination}");
        }
    }

    private string UserPrefsDir(string vamDir) => _fileSystem.Path.Combine(vamDir, "AddonPackagesUserPrefs");
}