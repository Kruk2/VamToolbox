using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Threading.Tasks.Dataflow;
using VamToolbox.Hashing;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Operations.NotDestructive;

public sealed class HashFilesOperation : IHashFilesOperation
{
    private readonly IProgressTracker _progressTracker;
    private readonly IHashingAlgo _hasher;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private readonly IDatabase _database;

    private readonly ConcurrentDictionary<(string fullPath, string localAssetPath), string> _newHashes = new();
    private readonly ConcurrentBag<string> _errors = new();
    private ConcurrentDictionary<(string fullPath, string localAssetPath), string> _hashes = new();
    private OperationContext _context = new();
    private int _scanned;
    private int _totalFiles;
    public HashFilesOperation(IProgressTracker progressTracker, IHashingAlgo hasher, IFileSystem fs, ILogger logger, IDatabase database)
    {
        _progressTracker = progressTracker;
        _hasher = hasher;
        _fs = fs;
        _logger = logger;
        _database = database;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        _context = context;
        _progressTracker.InitProgress("Hashing files");
        await _logger.Init("hash_files.log");

        _totalFiles = (varFiles?.Count ?? 0) + freeFiles.Count;

        _hashes = await _database.GetHashes();
        var scanPackageBlock = CreateBlock();
        foreach (var packageFile in varFiles ?? new List<VarPackage>())
            scanPackageBlock.Post(packageFile);

        scanPackageBlock.Complete();
        await scanPackageBlock.Completion;

        var scanFreeFilesPath = CreateFreeFilesBlock();
        foreach (var freeFile in freeFiles)
            scanFreeFilesPath.Post(freeFile);

        scanFreeFilesPath.Complete();
        await scanFreeFilesPath.Completion;

        await _database.AddHashes(_newHashes);

        foreach (var error in _errors)
        {
            _logger.Log(error);
        }

        _progressTracker.Complete($"Hashed {_scanned} vars and files. Found {_errors.Count} errors");
    }

    private ActionBlock<FreeFile> CreateFreeFilesBlock()
    {
        var scanPackageBlock = new ActionBlock<FreeFile>(
            ExecuteFreeFileOneAsync,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _context.Threads
            });
        return scanPackageBlock;
    }

    private async Task ExecuteFreeFileOneAsync(FreeFile freeFile)
    {
        var key = (fullPath: freeFile.FullPath, localAssetPath: "");
        if (_hashes.TryGetValue(key, out var hash))
        {
            freeFile.Hash = hash;
        }
        else
        {
            await using var stream = _fs.File.OpenRead(freeFile.FullPath);
            freeFile.Hash = await _hasher.GetHash(stream);
            _newHashes[key] = freeFile.Hash;
        }

        _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _totalFiles, freeFile.FilenameLower));
    }

    private ActionBlock<VarPackage> CreateBlock()
    {
        var scanPackageBlock = new ActionBlock<VarPackage>(
            ExecuteOneAsync,
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _context.Threads
            });
        return scanPackageBlock;
    }

    private async Task ExecuteOneAsync(VarPackage var)
    {
        try
        {
            await using var stream = File.OpenRead(var.FullPath);
            using var archive = new ZipArchive(stream);

            var archiveDict = archive.Entries.ToDictionary(t => t.FullName.NormalizePathSeparators());
            foreach (var entry in var.Files.SelectMany(t => t.SelfAndChildren()).Distinct())
            {
                entry.Hash = await HashFileAsync(entry, archiveDict[entry.LocalPath]);
            }

            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _totalFiles, var.Name.Filename));
        }
        catch (Exception e)
        {
            _errors.Add($"Unable to scan file {var.FullPath}. {e.Message}");
        }
    }

    private async Task<string> HashFileAsync(VarPackageFile file, ZipArchiveEntry entry)
    {
        var key = (fullPath: file.ParentVar.FullPath, localAssetPath: file.LocalPath);
        if (_hashes.TryGetValue(key, out var hash))
        {
            return hash;
        }

        await using var stream = entry.Open();
        hash = await _hasher.GetHash(stream);
        _newHashes[key] = hash;

        return hash;
    }
}

public interface IHashFilesOperation : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<VarPackage> varFiles, IList<FreeFile> freeFiles);
}