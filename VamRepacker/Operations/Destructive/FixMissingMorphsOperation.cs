using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;
using VamRepacker.Operations.NotDestructive;

namespace VamRepacker.Operations.Destructive;

public sealed class FixMissingMorphsOperation : IFixMissingMorphsOperation
{
    private readonly IProgressTracker _progressTracker;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private readonly IScanFilesOperation _fileScan;
    private OperationContext _context;
    private IList<FreeFile> _files;

    public FixMissingMorphsOperation(IProgressTracker progressTracker, IFileSystem fs, ILogger logger, IScanFilesOperation fileScan)
    {
        _progressTracker = progressTracker;
        _fs = fs;
        _logger = logger;
        _fileScan = fileScan;
    }

    public async Task ExecuteAsync(OperationContext context, IList<FreeFile> files, IList<VarPackage> vars)
    {
        _context = context;
        _files = files.Where(t => t.IsInVaMDir).ToList();
        await _logger.Init("fix_missing_morphs.log");
        _progressTracker.InitProgress("Fixing missing morphs");

        var fixedMorphs = await Task.Run(FixMissingMorphsThatHaveMatch);

        _files = await _fileScan.ExecuteAsync(context);
        _files = files.Where(t => t.IsInVaMDir).ToList();
        var unableToFix = await Task.Run(FixMissingMorphsThatDontHaveMatch);

        _progressTracker.Complete($"Fixed {fixedMorphs} morphs. Unable to fix: {unableToFix}.");
    }

    private int FixMissingMorphsThatHaveMatch()
    {
        var (missingMorphs, allMorphsByName) = GetMissingMorphsAndLookup();
        int fixedMorphs = 0;

        foreach (var missingMorph in missingMorphs)
        {
            _progressTracker.Report($"Processing: {missingMorph.LocalPath}");

            var missingMorphName = GetOppositeMorphName(missingMorph);

            var matchingMorphs = allMorphsByName[missingMorphName].DistinctBy(t => t.Size).ToList();
            if (matchingMorphs.Count == 1)
            {
                _logger.Log($"Found match for {missingMorph.LocalPath} as {matchingMorphs[0].LocalPath}");
                var destPath = _fs.Path.Combine(_fs.Path.GetDirectoryName(missingMorph.FullPath), missingMorphName);
                if (_fs.File.Exists(destPath))
                {
                    _logger.Log($"ERROR: Dest path already exists for: {destPath}");
                    continue;
                }

                fixedMorphs++;
                if (!_context.DryRun)
                    _fs.File.Copy(matchingMorphs[0].FullPath, destPath);
            }
        }

        return fixedMorphs;
    }

    private int FixMissingMorphsThatDontHaveMatch()
    {
        var (missingMorphs, allMorphsByName) = GetMissingMorphsAndLookup();
        int unableToFix = 0;
        var invalidMorphsDirectory = _fs.Path.Combine(_context.VamDir, "invalid_morphs");
        if (!_context.DryRun)
            _fs.Directory.CreateDirectory(invalidMorphsDirectory);

        foreach (var missingMorph in missingMorphs)
        {
            _progressTracker.Report($"Processing: {missingMorph.LocalPath}");

            var missingMorphName = GetOppositeMorphName(missingMorph);
            var matchingMorphs = allMorphsByName[missingMorphName].DistinctBy(t => t.Size).ToList();
            if (matchingMorphs.Count is 0 or > 1)
            {
                _logger.Log($"Unable to find {(matchingMorphs.Count > 1 ? "unique " : "")} match for {missingMorph.LocalPath}");

                unableToFix++;
                if (!_context.DryRun)
                {
                    var relativePath = _fs.Path.GetRelativePath(_context.VamDir, missingMorph.FullPath);
                    var destPath = _fs.Path.Combine(invalidMorphsDirectory, relativePath);
                    Directory.CreateDirectory(_fs.Path.GetDirectoryName(destPath));
                    _fs.File.Move(missingMorph.FullPath, destPath);
                }
            }
        }

        return unableToFix;
    }

    private string GetOppositeMorphName(FreeFile missingMorph)
    {
        var missingMorphName = missingMorph.ExtLower is ".vmi"
            ? _fs.Path.GetFileNameWithoutExtension(missingMorph.FullPath) + ".vmb"
            : _fs.Path.GetFileNameWithoutExtension(missingMorph.FullPath) + ".vmi";
        return missingMorphName;
    }

    private (IEnumerable<FreeFile> missingMorphs, ILookup<string, FreeFile> allMorphsByName) GetMissingMorphsAndLookup()
    {
        var missingMorphs = _files.Where(t => t.Type == AssetType.Morph && t.Children.Count == 0);
        var allMorphsByName = _files
            .SelectMany(t => t.SelfAndChildren())
            .Where(t => t.Type == AssetType.Morph)
            .ToLookup(t => t.FilenameLower);
        return (missingMorphs, allMorphsByName);
    }
}

public interface IFixMissingMorphsOperation : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<FreeFile> files, IList<VarPackage> vars);
}
 