//using System;
//using System.Collections.Generic;
//using System.Diagnostics.CodeAnalysis;
//using System.IO;
//using System.IO.Abstractions;
//using System.IO.Compression;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Threading.Tasks.Dataflow;
//using MoreLinq;
//using VamRepacker.Helpers;
//using VamRepacker.Logging;
//using VamRepacker.Models;
//using VamRepacker.Operations.Abstract;

//namespace VamRepacker.Operations.Destructive;

//public sealed class DeduplicateOperation : IDeduplicateOperation
//{
//    private ILookup<string, FileReferenceBase> _filesByHash = null!;
//    private readonly Dictionary<JsonFile, List<JsonUpdateDto>> _changesQueue = new();

//    private IList<VarPackage> _vars = null!;
//    private IList<ToFreeFile> _freeFiles = null!;
//    private OperationContext _context = null!;
//    private int _total;
//    private int _processed;

//    private long _removedBytes;
//    private readonly ILogger _logger;
//    private readonly IProgressTracker _progressTracker;
//    private readonly IJsonUpdater _jsonUpdater;
//    private readonly IFileSystem _fs;
//    private readonly Dictionary<VarPackage, List<FileReferenceBase>> _varDeleteQueue = new();
//    private readonly List<FileReferenceBase> _freeDeleteQueue = new();

//    public DeduplicateOperation(ILogger logger, IProgressTracker progressTracker, IJsonUpdater jsonUpdater, IFileSystem fs)
//    {
//        _logger = logger;
//        _progressTracker = progressTracker;
//        _jsonUpdater = jsonUpdater;
//        _fs = fs;
//    }

//    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<ToFreeFile> freeFiles)
//    {
//        _context = context with {DryRun = true };
//        _vars = vars;
//        _freeFiles = freeFiles;
//        _jsonUpdater.DryRun = context.DryRun;
//        _progressTracker.InitProgress("Deduplicating");

//        await Task.Run(CalculateDuplicates);
//        await RewriteJsonFiles();
//        await DeleteDuplicates();

//        _progressTracker.Complete($"Deduplicated successfully: {GetSizeDescription()}");
//    }

//    private string GetSizeDescription()
//    {
//        var gbSize = _removedBytes / 1024d / 1024d / 1024d;
//        var mbSize = _removedBytes / 1024d / 1024d - (int)gbSize * 1024d;
//        return $"{(int)gbSize} GB and {(int)mbSize} MB";
//    }

//    private async Task DeleteDuplicates()
//    {
//        var removeBlock = new ActionBlock<(VarPackage? varPackage, List<FileReferenceBase> toDelete)>(
//            RemoveDuplicates,
//            new ExecutionDataflowBlockOptions
//            {
//                MaxDegreeOfParallelism = _context.Threads
//            });

//        foreach (var (varPackage, toDelete) in _varDeleteQueue)
//        {
//            removeBlock.Post((varPackage, toDelete));
//        }
//        foreach (var f in _freeDeleteQueue)
//        {
//            removeBlock.Post((null, new List<FileReferenceBase>{f}));
//        }

//        removeBlock.Complete();
//        await removeBlock.Completion;
//    }

//    private async Task RemoveDuplicates((VarPackage? varPackage, List<FileReferenceBase> toDelete) arg)
//    {
//        var (varPackage, toDelete) = arg;
//        if (varPackage is null)
//        {
//            var singleFile = toDelete.Single();
//            if (!_context.DryRun)
//            {
//                _fs.ToFile.Delete(((ToFreeFile)singleFile).FullPath);
//            }

//            Interlocked.Add(ref _removedBytes, singleFile.Size);
//            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _processed), _total, $"3/3 Removing files. Removed {GetSizeDescription()}"));
//        }
//        else
//        {
//            var date = _fs.FileInfo.FromFileName(varPackage.FullPath).LastWriteTime;
//            {
//                await using var varFileStream = ToFile.Open(varPackage.FullPath, FileMode.Open,
//                    _context.DryRun ? FileAccess.Read : FileAccess.ReadWrite);
//                using var varArchive = new ZipArchive(varFileStream,
//                    _context.DryRun ? ZipArchiveMode.Read : ZipArchiveMode.Update);
//                var entries = varArchive.Entries.ToDictionary(t => t.FullName.NormalizePathSeparators());

//                foreach (var varPackageFile in toDelete.Cast<VarPackageFile>())
//                {
//                    if (!_context.DryRun)
//                    {
//                        entries[varPackageFile.LocalPath].Delete();
//                    }

//                    Interlocked.Add(ref _removedBytes, varPackageFile.Size);
//                    _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _processed), _total,
//                        $"3/3 Removing files. Removed {GetSizeDescription()}"));
//                }
//            }

//            if (!_context.DryRun)
//                _fs.ToFile.SetLastWriteTime(varPackage.FullPath, date);
//        }
//    }

//    private async Task RewriteJsonFiles()
//    {
//        _total = _changesQueue.Values.Sum(t => t.Count) + _freeDeleteQueue.Count + _varDeleteQueue.Sum(t => t.Value.Count);

//        var removeBlock = new ActionBlock<(VarPackage? varPackage, List<(JsonFile jsonFile, List<JsonUpdateDto> dupes)>)>(
//            RewriteJsonFiles,
//            new ExecutionDataflowBlockOptions
//            {
//                MaxDegreeOfParallelism = _context.Threads
//            });

//        foreach (var group in _changesQueue.Where(t => t.Key.ToFile.IsVar).GroupBy(t => t.Key.ToFile.Var))
//        {
//            var list = group.Select(t => (t.Key, t.Value)).ToList();
//            removeBlock.Post((group.Key, list));
//        }

//        foreach (var (jsonFile, dupes) in _changesQueue.Where(t => !t.Key.ToFile.IsVar))
//        {
//            var list = new List<(JsonFile jsonFile, List<JsonUpdateDto> dupes)>
//            {
//                (jsonFile, dupes)
//            };
//            removeBlock.Post((null, list));
//        }

//        removeBlock.Complete();
//        await removeBlock.Completion;
//    }

//    private async Task RewriteJsonFiles((VarPackage? varPackage, List<(JsonFile jsonFile, List<JsonUpdateDto> dupes)> jsonDupes) arg)
//    {
//        var (varPackage, jsonDupes) = arg;
//        if (varPackage is null)
//        {
//            await RewriteJsonFiles(jsonDupes.Single());
//            return;
//        }

//        var date = _fs.FileInfo.FromFileName(varPackage.FullPath).LastWriteTime;
//        {
//            await using var varFileStream = ToFile.Open(varPackage.FullPath, FileMode.Open,
//                _context.DryRun ? FileAccess.Read : FileAccess.ReadWrite);
//            using var varArchive = new ZipArchive(varFileStream,
//                _context.DryRun ? ZipArchiveMode.Read : ZipArchiveMode.Update);
//            var entries = varArchive.Entries.ToDictionary(t => t.FullName.NormalizePathSeparators());

//            foreach (var (jsonFile, dupes) in jsonDupes)
//            {
//                await _jsonUpdater.UpdateVarJson(jsonFile, dupes, varArchive, entries);
//                _progressTracker.Report(new ProgressInfo(Interlocked.Add(ref _processed, dupes.Count), _total,
//                    $"2/3 Fixing json files in {varPackage.Name.Filename}"));
//            }
//        }

//        if(!_context.DryRun)
//            _fs.ToFile.SetLastWriteTime(varPackage.FullPath, date);
//    }

//    private async Task RewriteJsonFiles((JsonFile jsonFile, List<JsonUpdateDto> dupes) arg)
//    {
//        var (jsonFile, dupes) = arg;
//        if (jsonFile.ToFile.IsVar) throw new ArgumentException($"Json file expected to be in var {jsonFile}", nameof(arg));

//        var date = _fs.FileInfo.FromFileName(jsonFile.ToFile.Free.FullPath).LastWriteTime;
//        await _jsonUpdater.UpdateFreeJson(jsonFile, dupes);
//        _progressTracker.Report(new ProgressInfo(Interlocked.Add(ref _processed, dupes.Count), _total, $"2/3 Fixing json file in {jsonFile.ToFile.Free.LocalPath}"));

//        if (!_context.DryRun)
//            _fs.ToFile.SetLastWriteTime(jsonFile.ToFile.Free.FullPath, date);
//    }

//    private void CalculateDuplicates()
//    {
//        _filesByHash = _vars
//            .SelectMany(t => t.Files).Cast<FileReferenceBase>()
//            .Concat(_freeFiles)
//            .Distinct()
//            .Where(t => t.Size > 0)
//            .ToLookup(t => t.HashWithChildren);
//        var total = _filesByHash.Count(t => t.Count() > 1);
//        int processed = 0;

//        foreach (var fileReferences in _filesByHash.Where(t => t.Count() > 1))
//        {
//            IEnumerable<FileReferenceBase> filesToScan = fileReferences;
//            var freeFiles = fileReferences.OfType<ToFreeFile>();
//            if (freeFiles.Any())
//                filesToScan = freeFiles;

//            var fileToKeep = MoreEnumerable
//                .MaxBy(filesToScan, t => t.JsonReferences.Count)
//                .OrderBy(t => t.LocalPath)
//                .First();

//            foreach (var fileReference in fileReferences.Where(t => !ReferenceEquals(fileToKeep, t)))
//            {
//                QueueDuplicate(fileReference, fileToKeep);
//            }

//            _progressTracker.Report(new ProgressInfo(++processed, total, $"1/3 Calculating duplicates for {fileToKeep}"));
//        }
//    }

//    private void QueueDuplicate(FileReferenceBase toDelete, FileReferenceBase destination)
//    {
//        if (toDelete.ExtLower is not (".vaj" or ".vam" or ".vab" or ".vmi" or ".vmb" or ".jpg" or ".jpeg" or ".png" or ".tif" or ".assetbundle" or ".tiff" or ".dsf"))
//            return;

//        foreach (var jsonReference in toDelete.JsonReferences)
//        {
//            if (!_changesQueue.TryGetValue(jsonReference.FromJson, out var referencesToUpdate))
//            {
//                referencesToUpdate = new List<JsonUpdateDto>();
//                _changesQueue[jsonReference.FromJson] = referencesToUpdate;
//            }

//            referencesToUpdate.Add(new JsonUpdateDto(jsonReference.Reference, destination));
//        }

//        if (toDelete is VarPackageFile varFile)
//        {
//            if (!_varDeleteQueue.TryGetValue(varFile.ToParentVar, out var filesToDelete))
//            {
//                filesToDelete = new List<FileReferenceBase>();
//                _varDeleteQueue[varFile.ToParentVar] = filesToDelete;
//            }

//            foreach (var referenceBase in toDelete.Children.Where(t => t.JsonReferences.Count == 0))
//                filesToDelete.Add(referenceBase);

//            filesToDelete.Add(varFile);
//        }
//        else
//        {
//            foreach (var referenceBase in toDelete.Children.Where(t => t.JsonReferences.Count == 0))
//                _freeDeleteQueue.Add(referenceBase);

//            _freeDeleteQueue.Add(toDelete);
//        }
//    }
//}

//public interface IDeduplicateOperation : IOperation
//{
//    Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<ToFreeFile> freeFiles);
//}