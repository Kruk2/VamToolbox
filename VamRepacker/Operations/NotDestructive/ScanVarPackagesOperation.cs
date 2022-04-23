using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Autofac;
using MoreLinq;
using Newtonsoft.Json;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;
using VamRepacker.Sqlite;

namespace VamRepacker.Operations.NotDestructive;

public sealed class ScanVarPackagesOperation : IScanVarPackagesOperation
{
    private readonly IFileSystem _fs;
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;
    private readonly ILifetimeScope _scope;
    private readonly IDatabase _database;
    private readonly ISoftLinker _softLinker;
    private ILookup<string, (string basePath, FileReferenceBase file)> _favMorphs = null!;
    private readonly ConcurrentBag<VarPackage> _packages = new();
    private readonly VarScanResults _result = new ();

    private int _scanned;
    private int _totalVarsCount;
    private OperationContext _context = null!;

    public ScanVarPackagesOperation(IFileSystem fs, IProgressTracker progressTracker, ILogger logger, ILifetimeScope scope, IDatabase database, ISoftLinker softLinker)
    {
        _fs = fs;
        _reporter = progressTracker;
        _logger = logger;
        _scope = scope;
        _database = database;
        _softLinker = softLinker;
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
        foreach (var packageFile in packageFiles)
        {
            if (!VarPackageName.TryGet(_fs.Path.GetFileName(packageFile), out var name))
            {
                _result.InvalidVarName.Add(packageFile);
                continue;
            }

            scanPackageBlock.Post((packageFile, name));
        }

        scanPackageBlock.Complete();
        await scanPackageBlock.Completion;

        _result.Vars = _packages
            .GroupBy(t => t.Name.Filename)
            .Select(t =>
            {
                var sortedVars = t.OrderBy(t => t.FullPath).ToList();
                if (sortedVars.Count == 1) return sortedVars[0];

                _result.DuplicatedVars.Add(sortedVars.Select(t => t.FullPath).ToList());

                var fromVamDir = sortedVars.FirstOrDefault(t => t.IsInVaMDir);
                return fromVamDir ?? sortedVars.First();
            })
            .ToList();

        _reporter.Report("Updating local database", forceShow: true);
        await Task.Run(() => LookupDirtyPackages(_result.Vars));

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

    private Task<List<string>> InitLookups(IEnumerable<FreeFile> freeFiles)
    {
        return Task.Run(() =>
        {
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
            return packageFiles;
        });
    }

    private ActionBlock<(string, VarPackageName)> CreateBlock()
    {
        var scanPackageBlock = new ActionBlock<(string, VarPackageName)>(
            f => ExecuteOneAsync(f.Item1, f.Item2),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _context.Threads
            });
        return scanPackageBlock;
    }

    private async Task ExecuteOneAsync(string varFullPath, VarPackageName name)
    {
        try
        {
            varFullPath = varFullPath.NormalizePathSeparators();
            var files = new List<VarPackageFile>();
            await using var stream = _fs.File.OpenRead(varFullPath);
            using var archive = new ZipArchive(stream);

            var foundMetaFile = false;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue;
                if (entry.FullName == "meta.json")
                {
                    foundMetaFile = true;
                    continue;
                }

                var packageFile = ReadPackageFileAsync(entry);
                files.Add(packageFile);
            }
            if (!foundMetaFile)
            {
                _result.MissingMetaJson.Add(varFullPath);
                return;
            }

            var entries = archive.Entries.ToDictionary(t => t.FullName.NormalizePathSeparators());
            Stream OpenFileStream(string p) => entries[p].Open();

            await _scope.Resolve<IScriptGrouper>().GroupCslistRefs(files, OpenFileStream);
            await _scope.Resolve<IMorphGrouper>().GroupMorphsVmi(files, name, OpenFileStream, _favMorphs);
            await _scope.Resolve<IPresetGrouper>().GroupPresets(files, name, OpenFileStream);
            _scope.Resolve<IPreviewGrouper>().GroupsPreviews(files);

            var softLinkPath = _softLinker.GetSoftLink(varFullPath);
            var fileInfo = _fs.FileInfo.FromFileName(softLinkPath ?? varFullPath);
            var varPackage = new VarPackage(name, varFullPath, files, varFullPath.StartsWith(_context.VamDir, StringComparison.Ordinal), fileInfo.Length, fileInfo.LastWriteTimeUtc);
            files.SelectMany(t => t.SelfAndChildren()).ForEach(t => t.ParentVar = varPackage);
            _packages.Add(varPackage);
        }
        catch (Exception exc)
        {
            var message = $"{varFullPath}: {exc.Message}";
            _result.InvalidVars.Add(message);
        }

        _reporter.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _totalVarsCount, name.Filename));
    }

    private void LookupDirtyPackages(List<VarPackage> varPackages)
    {
        foreach (var varPackage in varPackages)
        {
            var (size, timeStamp) = _database.GetFileInfo(varPackage.FullPath);
            if (size == null || varPackage.Size != size.Value || timeStamp != varPackage.ModifiedTimestamp)
            {
                varPackage.Dirty = true;
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

    private static VarPackageFile ReadPackageFileAsync(ZipArchiveEntry entry) => new (entry.FullName.NormalizePathSeparators(), entry.Length);
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