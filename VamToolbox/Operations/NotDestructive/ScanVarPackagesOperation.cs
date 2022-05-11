using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Threading.Tasks.Dataflow;
using Autofac;
using Newtonsoft.Json;
using VamToolbox.FilesGrouper;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Operations.NotDestructive;

public sealed class ScanVarPackagesOperation : IScanVarPackagesOperation
{
    private readonly IFileSystem _fs;
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;
    private readonly ILifetimeScope _scope;
    private readonly ISoftLinker _softLinker;
    private ILookup<string, (string basePath, FileReferenceBase file)> _favMorphs = null!;
    private readonly ConcurrentBag<VarPackage> _packages = new();
    private readonly VarScanResults _result = new();

    private int _scanned;
    private int _totalVarsCount;
    private OperationContext _context = null!;
    private readonly IDatabase _database;
    private Dictionary<string, Dictionary<string, (long size, DateTime modifiedTime, string? uuid)>> _uuidCache = null!;

    public ScanVarPackagesOperation(IFileSystem fs, IProgressTracker progressTracker, ILogger logger, ILifetimeScope scope, ISoftLinker softLinker, IDatabase database)
    {
        _fs = fs;
        _reporter = progressTracker;
        _logger = logger;
        _scope = scope;
        _softLinker = softLinker;
        _database = database;
    }

    public async Task<List<VarPackage>> ExecuteAsync(OperationContext context, IEnumerable<FreeFile> freeFiles)
    {
        _context = context;
        _reporter.InitProgress("Scanning var files");
        await _logger.Init("var_scan.log");

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var packageFiles = await InitLookups(freeFiles);

        var scanPackageBlock = CreateBlock();
        foreach (var packageFile in packageFiles) {
            if (!VarPackageName.TryGet(_fs.Path.GetFileName(packageFile.path), out var name)) {
                _result.InvalidVarName.Add(packageFile.path);
                continue;
            }

            scanPackageBlock.Post((packageFile.path, packageFile.softLink, name));
        }

        scanPackageBlock.Complete();
        await scanPackageBlock.Completion;

        _result.Vars = _packages
            .GroupBy(t => t.Name.Filename)
            .Select(t => {
                var sortedVars = t.OrderBy(t => t.FullPath).ToList();
                if (sortedVars.Count == 1) return sortedVars[0];

                _result.DuplicatedVars.Add(sortedVars.Select(t => t.FullPath).ToList());

                var fromVamDir = sortedVars.FirstOrDefault(t => t.IsInVaMDir);
                return fromVamDir ?? sortedVars.First();
            })
            .ToList();

        var endingMessage = $"Found {_result.Vars.SelectMany(t => t.Files).Count()} files in {_result.Vars.Count} var packages. Took {stopWatch.Elapsed:hh\\:mm\\:ss}. Check var_scan.log";
        _reporter.Complete(endingMessage);

        foreach (var err in _result.InvalidVarName.OrderBy(t => t))
            _logger.Log($"[INVALID-VAR-NAME] {err}");
        foreach (var err in _result.MissingMetaJson.OrderBy(t => t))
            _logger.Log($"[MISSING-META-JSON] {err}");
        foreach (var err in _result.InvalidVars.OrderBy(t => t))
            _logger.Log($"[INVALID-VAR] {err}");
        foreach (var err in _result.DuplicatedVars)
            _logger.Log($"[DUPLICATED-VARS] {Environment.NewLine} {string.Join(Environment.NewLine, err)}");
        return _result.Vars;
    }

    private Task<IEnumerable<(string path, string? softLink)>> InitLookups(IEnumerable<FreeFile> freeFiles)
    {
        return Task.Run(() => {
            var packageFiles = _fs.Directory
                .GetFiles(_fs.Path.Combine(_context.VamDir, "AddonPackages"), "*.var", SearchOption.AllDirectories)
                .ToList();

            if (!string.IsNullOrEmpty(_context.RepoDir))
                packageFiles.AddRange(_fs.Directory.GetFiles(_context.RepoDir, "*.var", SearchOption.AllDirectories));

            _totalVarsCount = packageFiles.Count;
            var favDirs = KnownNames.MorphDirs.Select(t => Path.Combine(t, "favorites").NormalizePathSeparators()).ToArray();
            _favMorphs = freeFiles
                .Where(t => t.ExtLower == ".fav" && favDirs.Any(x => t.LocalPath.StartsWith(x, StringComparison.Ordinal)))
                .ToLookup(t => t.FilenameWithoutExt,
                    t => (basePath: Path.GetDirectoryName(t.LocalPath)!.NormalizePathSeparators(), file: (FileReferenceBase)t));

            _uuidCache = _database.ReadVarFilesCache()
                .GroupBy(t => t.fullPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    t => t.Key,
                    t => t.ToDictionary(x => x.localPath, x => (x.size, x.modifiedTime, x.uuid)),
                    StringComparer.OrdinalIgnoreCase);

            return packageFiles
                .Select(t => (path: t, softLink: _softLinker.GetSoftLink(t)))
                .Where(t => t.softLink is null || _fs.File.Exists(t.softLink));
        });
    }

    private ActionBlock<(string path, string? softLink, VarPackageName varName)> CreateBlock()
    {
        var scanPackageBlock = new ActionBlock<(string path, string? softLink, VarPackageName varName)>(
            f => ExecuteOneAsync(f.path, f.softLink, f.varName),
            new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = _context.Threads
            });
        return scanPackageBlock;
    }

    private async Task ExecuteOneAsync(string varFullPath, string? softLink, VarPackageName name)
    {
        try {
            varFullPath = varFullPath.NormalizePathSeparators();
            var isInVamDir = varFullPath.StartsWith(_context.VamDir, StringComparison.Ordinal);
            var fileInfo = softLink != null ? _fs.FileInfo.FromFileName(softLink) : _fs.FileInfo.FromFileName(varFullPath);
            var varPackage = new VarPackage(name, varFullPath, softLink, isInVamDir, fileInfo.Length);

            await using var stream = _fs.File.OpenRead(varFullPath);
            using var archive = new ZipArchive(stream);

            var foundMetaFile = false;
            foreach (var entry in archive.Entries) {
                if (entry.FullName.EndsWith('/')) continue;
                if (entry.FullName == "meta.json") {
                    foundMetaFile = true;
                    continue;
                }

                CreatePackageFileAsync(entry, isInVamDir, entry.LastWriteTime.DateTime, varPackage);
            }
            if (!foundMetaFile) {
                _result.MissingMetaJson.Add(varFullPath);
                return;
            }


            var varFilesList = (List<VarPackageFile>)varPackage.Files;
            _packages.Add(varPackage);

            var entries = archive.Entries.ToDictionary(t => t.FullName.NormalizePathSeparators());
            Stream OpenFileStream(string p) => entries[p].Open();
            LookupDirtyPackages(varPackage);

            await _scope.Resolve<IScriptGrouper>().GroupCslistRefs(varFilesList, OpenFileStream);
            await _scope.Resolve<IMorphGrouper>().GroupMorphsVmi(varFilesList, name, OpenFileStream, _favMorphs);
            await _scope.Resolve<IPresetGrouper>().GroupPresets(varFilesList, name, OpenFileStream);
            _scope.Resolve<IPreviewGrouper>().GroupsPreviews(varFilesList);
        } catch (Exception exc) {
            var message = $"{varFullPath}: {exc.Message}";
            _result.InvalidVars.Add(message);
        }

        _reporter.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _totalVarsCount, name.Filename));
    }

    private void LookupDirtyPackages(VarPackage varPackage)
    {
        foreach (var varFile in varPackage.Files
                     .SelectMany(t => t.SelfAndChildren())
                     .Where(t => t.ExtLower is ".vmi" or ".vam" || KnownNames.IsPotentialJsonFile(t.ExtLower) && t.FilenameLower != "meta.json")) {
            if (!_uuidCache.TryGetValue(varPackage.FullPath, out var cacheEntry) ||
                !cacheEntry.TryGetValue(varFile.LocalPath, out var uuidEntry)) {
                varFile.Dirty = true;
                continue;
            }

            if (varFile.Size != uuidEntry.size || uuidEntry.modifiedTime != varFile.ModifiedTimestamp) {
                varFile.Dirty = true;
            } else if (!string.IsNullOrEmpty(uuidEntry.uuid)) {
                if (varFile.ExtLower == ".vmi") {
                    varFile.MorphName = uuidEntry.uuid;
                } else if (varFile.ExtLower == ".vam") {
                    varFile.InternalId = uuidEntry.uuid;
                }
            }
        }
    }

    private static async Task<MetaFileJson?> ReadMetaFile(ZipArchiveEntry metaEntry)
    {
        await using var metaStream = metaEntry.Open();
        using var sr = new StreamReader(metaStream);
        using var reader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<MetaFileJson>(reader);
    }

    private static void CreatePackageFileAsync(ZipArchiveEntry entry, bool isInVamDir, DateTime modifiedTimestamp, VarPackage varPackage)
    {
        _ = new VarPackageFile(entry.FullName.NormalizePathSeparators(), entry.Length, isInVamDir, varPackage, modifiedTimestamp);
    }
}

public interface IScanVarPackagesOperation : IOperation
{
    Task<List<VarPackage>> ExecuteAsync(OperationContext context, IEnumerable<FreeFile> freeFiles);
}

public class VarScanResults
{
    public List<VarPackage> Vars { get; set; } = new();
    public ConcurrentBag<string> InvalidVars { get; } = new();
    public ConcurrentBag<string> InvalidVarName { get; } = new();
    public ConcurrentBag<string> MissingMetaJson { get; } = new();

    public ConcurrentBag<string> MissingMorphsFiles { get; } = new();
    public ConcurrentBag<string> MissingPresetsFiles { get; } = new();
    public ConcurrentBag<string> MissingScriptFiles { get; } = new();
    public List<List<string>> DuplicatedVars { get; set; } = new();
}