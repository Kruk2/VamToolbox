using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.Destructive;

public class RemoveSoftLinks : IRemoveSoftLinks
{
    private readonly IProgressTracker _progressTracker;
    private readonly ISoftLinker _softLinker;
    private readonly ILogger _logger;
    private OperationContext _context;

    public RemoveSoftLinks(IProgressTracker progressTracker, ISoftLinker softLinker, ILogger logger)
    {
        _progressTracker = progressTracker;
        _softLinker = softLinker;
        _logger = logger;
    }

    public async Task ExecuteAsync(OperationContext context)
    {
        _context = context;

        _progressTracker.InitProgress("Removing soft-links");
        int softLinksRemoved = 0;
        var addonDir = Path.Combine(context.VamDir, "AddonPackages");

        await Task.Run(() =>
        {
            var softLinks = Directory
                .EnumerateFiles(context.VamDir, "*.*", SearchOption.AllDirectories)
                .Where(t => _softLinker.IsSoftLink(t));

            foreach (var softLink in softLinks)
            {
                if (!_context.DryRun) File.Delete(softLink);

                Interlocked.Increment(ref softLinksRemoved);
                _progressTracker.Report(Path.GetFileName(softLink));
            }

            RemoveEmptyDirs(addonDir);
        });

        _progressTracker.Complete($"Removed {softLinksRemoved} softlinks");
    }

    private void RemoveEmptyDirs(string startLocation)
    {
        foreach (var directory in Directory.GetDirectories(startLocation))
        {
            RemoveEmptyDirs(directory);
            if (Directory.GetFiles(directory).Length == 0 &&
                Directory.GetDirectories(directory).Length == 0)
            {
                Directory.Delete(directory, false);
            }
        }
    }
}

public interface IRemoveSoftLinks : IOperation
{
    Task ExecuteAsync(OperationContext context);
}