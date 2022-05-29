using System.IO.Abstractions;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Destructive;

public sealed class RemoveSoftLinksAndEmptyDirs : IRemoveSoftLinksAndEmptyDirs
{
    private readonly IProgressTracker _progressTracker;
    private readonly ISoftLinker _softLinker;
    private readonly ILogger _logger;
    private readonly IFileSystem _fs;
    private OperationContext _context = null!;

    public RemoveSoftLinksAndEmptyDirs(IProgressTracker progressTracker, ISoftLinker softLinker, ILogger logger, IFileSystem fs)
    {
        _progressTracker = progressTracker;
        _softLinker = softLinker;
        _logger = logger;
        _fs = fs;
    }

    public async Task ExecuteAsync(OperationContext context)
    {
        _context = context;

        _progressTracker.InitProgress("Removing soft-links");
        int softLinksRemoved = 0;

        await Task.Run(() => {

            var dirsToScan = KnownNames.KnownDirs
                .Append("AddonPackages")
                .Select(t => _fs.Path.Combine(_context.VamDir, t))
                .Where(_fs.Directory.Exists)
                .ToArray();

            var softLinks = dirsToScan
                .SelectMany(t => _fs.Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                .Where(_softLinker.IsSoftLink);

            foreach (var softLink in softLinks) {
                if (!_context.DryRun) _fs.File.Delete(softLink);

                Interlocked.Increment(ref softLinksRemoved);
                _progressTracker.Report(_fs.Path.GetFileName(softLink));
            }

            foreach (var startLocation in dirsToScan) {
                _logger.Log($"Cleanup dir: {startLocation}");
                if (!_context.DryRun) {
                    RemoveEmptyDirs(startLocation);
                }
            }
        });

        _progressTracker.Complete($"Removed {softLinksRemoved} softlinks");
    }

    private void RemoveEmptyDirs(string startLocation)
    {
        foreach (var directory in _fs.Directory.GetDirectories(startLocation)) {
            RemoveEmptyDirs(directory);
            if (_fs.Directory.GetFiles(directory).Length == 0 &&
                _fs.Directory.GetDirectories(directory).Length == 0) {
                _fs.Directory.Delete(directory, false);
            }
        }
    }
}

public interface IRemoveSoftLinksAndEmptyDirs : IOperation
{
    Task ExecuteAsync(OperationContext context);
}