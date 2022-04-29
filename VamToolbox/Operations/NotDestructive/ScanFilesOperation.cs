using System.IO.Abstractions;
using Autofac;
using VamToolbox.FilesGrouper;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Operations.NotDestructive;

public sealed class ScanFilesOperation : IScanFilesOperation
{
    private readonly IProgressTracker _reporter;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private readonly ILifetimeScope _scope;
    private readonly IDatabase _database;
    private OperationContext _context = null!;
    private readonly ISoftLinker _softLinker;
    private Dictionary<string, (long size, DateTime modifiedTime, string? uuid)> _uuidCache = null!;

    public ScanFilesOperation(IProgressTracker reporter, IFileSystem fs, ILogger logger, ILifetimeScope scope, IDatabase database, ISoftLinker softLinker)
    {
        _reporter = reporter;
        _fs = fs;
        _logger = logger;
        _scope = scope;
        _database = database;
        _softLinker = softLinker;
    }

    public async Task<List<FreeFile>> ExecuteAsync(OperationContext context)
    {
        _reporter.InitProgress("Scanning files");
        await _logger.Init("scan_files.log");
        _context = context;

        var files = await ScanFolder(_context.VamDir);
        if(!string.IsNullOrEmpty(_context.RepoDir))
        {
            files.AddRange(await ScanFolder(_context.RepoDir));
        }

        _reporter.Complete($"Scanned {files.Count} files in the Saves and Custom folders. Check scan_files.log");

        return files;
    }

    private async Task<List<FreeFile>> ScanFolder(string rootDir)
    {
        var files = new List<FreeFile>();

        await Task.Run(async () =>
        {
            _uuidCache = _database.ReadFreeFilesCache().ToDictionary(t => t.fullPath, t => (t.size, t.modifiedTime, t.uuid), StringComparer.OrdinalIgnoreCase);

            _reporter.Report("Scanning Custom folder", forceShow: true);
            files.AddRange(ScanFolder(rootDir, "Custom"));
            _reporter.Report("Scanning Saves folder", forceShow: true);
            files.AddRange(ScanFolder(rootDir, "Saves"));

            _reporter.Report("Analyzing fav files", forceShow: true);
            var favDirs = KnownNames.MorphDirs.Select(t => Path.Combine(t, "favorites").NormalizePathSeparators()).ToArray();
            var favMorphs = files
                .Where(t => t.ExtLower == ".fav" && favDirs.Any(x => t.LocalPath.StartsWith(x, StringComparison.Ordinal)))
                .ToLookup(t => t.FilenameWithoutExt, t => (basePath: Path.GetDirectoryName(t.LocalPath)!.NormalizePathSeparators(), file: (FileReferenceBase)t));
                    
            Stream OpenFileStream(string p) => _fs.File.OpenRead(_fs.Path.Combine(rootDir, p));

            _reporter.Report("Updating local database", forceShow: true);
            LookupDirtyFiles(files);

            _reporter.Report("Grouping scripts", forceShow: true);
            await _scope.Resolve<IScriptGrouper>().GroupCslistRefs(files, OpenFileStream);
            _reporter.Report("Grouping morphs", forceShow: true);
            await _scope.Resolve<IMorphGrouper>().GroupMorphsVmi(files, varName: null, openFileStream: OpenFileStream, favMorphs);
            _reporter.Report("Grouping presets", forceShow: true);
            await _scope.Resolve<IPresetGrouper>().GroupPresets(files, varName: null, OpenFileStream);
            _reporter.Report("Grouping previews", forceShow: true);
            _scope.Resolve<IPreviewGrouper>().GroupsPreviews(files);

        });

        return files;
    }

    private IEnumerable<FreeFile> ScanFolder(string rootDir, string folder)
    {
        var searchDir = _fs.Path.Combine(rootDir, folder);
        if (!Directory.Exists(searchDir))
            return Enumerable.Empty<FreeFile>();

        var isVamDir = _context.VamDir == rootDir;
        var files = _fs.Directory
            .EnumerateFiles(searchDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\."))
            .Select(f => (path: f, softLink: _softLinker.GetSoftLink(f)))
            .Where(f => f.softLink is null || File.Exists(f.softLink))
            .Select(f => (f.path, fileInfo: _fs.FileInfo.FromFileName(f.softLink ?? f.path)))
            .Select(f => new FreeFile(f.path, f.path.RelativeTo(rootDir), f.fileInfo.Length, isVamDir, f.fileInfo.LastWriteTimeUtc))
            .ToList();

        return files;
    }

    private void LookupDirtyFiles(List<FreeFile> files)
    {
        foreach (var freeFile in files
                     .SelectMany(t => t.SelfAndChildren())
                     .Where(t => t.ExtLower is ".vmi" or ".vam" || KnownNames.IsPotentialJsonFile(t.ExtLower)))
        {
            if (!_uuidCache.TryGetValue(freeFile.FullPath, out var uuidEntry))
            {
                freeFile.Dirty = true;
                continue;
            }

            if (freeFile.Size != uuidEntry.size || uuidEntry.modifiedTime != freeFile.ModifiedTimestamp)
            {
                freeFile.Dirty = true;
            }
            else if (!string.IsNullOrEmpty(uuidEntry.uuid))
            {
                if (freeFile.ExtLower == ".vmi")
                {
                    freeFile.MorphName = uuidEntry.uuid;
                }
                else if (freeFile.ExtLower == ".vam")
                {
                    freeFile.InternalId = uuidEntry.uuid;
                }
            }
        }
    }
}

public interface IScanFilesOperation : IOperation
{
    Task<List<FreeFile>> ExecuteAsync(OperationContext context);
}