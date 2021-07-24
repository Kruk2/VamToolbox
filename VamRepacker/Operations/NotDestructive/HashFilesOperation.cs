using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VamRepacker.Hashing;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.NotDestructive
{
    public class HashFilesOperation : IHashFilesOperation
    {
        private readonly IProgressTracker _progressTracker;
        private readonly IHashingAlgo _hasher;
        private readonly IFileSystem _fs;
        private readonly ILogger _logger;
        private readonly IDatabase _database;

        private ConcurrentDictionary<Database.HashesTable, string> _hashes;
        private readonly ConcurrentBag<Database.HashesTable> _newHashes = new();

        public HashFilesOperation(IProgressTracker progressTracker, IHashingAlgo hasher, IFileSystem fs, ILogger logger, IDatabase database)
        {
            _progressTracker = progressTracker;
            _hasher = hasher;
            _fs = fs;
            _logger = logger;
            _database = database;
        }

        private int _scanned;
        private int _totalFiles;
        private readonly ConcurrentBag<string> _errors = new();
        private OperationContext _context;

        public async Task ExecuteAsync(OperationContext context, IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
        {
            _context = context;
            _database.Open(context.VamDir);
            _progressTracker.InitProgress();
            _logger.Init("hash_files.log");

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
            var lookup = new Database.HashesTable { LocalAssetPath = freeFile.FullPath, VarFileName = "" };
            if (_hashes.TryGetValue(lookup, out var hash))
            {
                freeFile.Hash = hash;
            }
            else
            {
                await using var stream = _fs.File.OpenRead(freeFile.FullPath);
                freeFile.Hash = await _hasher.GetHash(stream);
                lookup.Hash = freeFile.Hash;
                _newHashes.Add(lookup);
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
            var lookup = new Database.HashesTable { LocalAssetPath = file.LocalPath, VarFileName = file.ParentVar.Name.Filename };
            if (_hashes.TryGetValue(lookup, out var hash))
            {
                return hash;
            }

            await using var stream = entry.Open();
            lookup.Hash = await _hasher.GetHash(stream);
            _newHashes.Add(lookup);

            return lookup.Hash;
        }
    }

    public interface IHashFilesOperation : IOperation
    {
        Task ExecuteAsync(OperationContext context, IList<VarPackage> varFiles, IList<FreeFile> freeFiles);
    }
}
