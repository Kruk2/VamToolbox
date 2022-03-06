using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FuzzySharp;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.Destructive
{
    public class FixJsonDependenciesOperation : IFixJsonDependenciesOperation
    {
        private readonly IProgressTracker _progressTracker;
        private readonly ILogger _logger;
        private readonly IFileSystem _fs;
        private ILookup<string, FileReferenceBase> _filesByName;
        private ILookup<string, FileReferenceBase> _filesByPath;
        private IDictionary<string, ILookup<string, VarPackageFile>> _varsByAuthor;
        private readonly ConcurrentDictionary<string, string> _filesToCopy = new();
        private OperationContext _context;


        public FixJsonDependenciesOperation(IProgressTracker progressTracker, ILogger logger, IFileSystem fs)
        {
            _progressTracker = progressTracker;
            _logger = logger;
            _fs = fs;
        }

        public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, IList<JsonFile> jsonFiles, bool makeBackup)
        {
            _context = context;
            _progressTracker.InitProgress("Fixing json dependencies");
            InitLookups(vars, freeFiles);

            var jsonWithMissingReferences = jsonFiles
                .Where(t => (t.IsVar && t.Var.IsInVaMDir) || !t.IsVar)
                .Where(t => t.Missing.Count > 0)
                .ToList();

            foreach (var x in jsonWithMissingReferences)
            {
                await FixReferencesOneAsync(x);
            }
        }

        private string ResolveDestDirConflicts(VarPackageFile bestReference, string destDir)
        {
            lock (_filesToCopy)
            {
                var index = 0;
                IEnumerable<string> destinations;
                var originalDirName = Path.GetFileName(destDir);

                while (true)
                {
                    destinations = bestReference.SelfAndChildren()
                        .Select(t => Path.GetRelativePath(Path.GetDirectoryName(bestReference.LocalPath), t.LocalPath))
                        .Select(t => Path.Combine(destDir, t).NormalizePathSeparators());

                    var hasFileConflicts = destinations
                        .Any(File.Exists);
                    if (!hasFileConflicts)
                        break;

                    destDir = Path.Combine(Path.GetDirectoryName(destDir), originalDirName + $"_{index++}").NormalizePathSeparators();
                }

                foreach (var dest in destinations)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (File.Create(dest))
                    {
                    }
                }
            }

            return destDir;
        }

        private async Task<string> CopyVarFile(VarPackageFile bestReference)
        {
            if (_filesToCopy.TryAdd(bestReference.HashWithChildren, string.Empty))
            {
                var parentDir = Path.GetDirectoryName(bestReference.LocalPath).NormalizePathSeparators();
                var destDir = Path.Combine(_context.VamDir, Path.GetDirectoryName(bestReference.LocalPath));
                if (KnownNames.KnownVamDirs.Contains(parentDir, StringComparer.InvariantCultureIgnoreCase))
                    destDir = Path.Combine(_context.VamDir, parentDir, bestReference.ParentVar.Name.Author.RemoveInvalidChars()).NormalizePathSeparators();

                destDir = ResolveDestDirConflicts(bestReference, destDir);

                var withChildren = bestReference.SelfAndChildren();
                await using var varFileStream = File.OpenRead(bestReference.ParentVar.FullPath);
                using var varArchive = new ZipArchive(varFileStream);

                foreach (var varFile in withChildren)
                {
                    var localToParent = Path.GetRelativePath(parentDir, varFile.LocalPath);
                    var destFile = Path.Combine(destDir, localToParent).NormalizePathSeparators();

                    var entry = varArchive.Entries.First(t => t.FullName.NormalizePathSeparators() == varFile.LocalPath);

                    if(_context.DryRun)
                        continue;

                    await using var stream = entry.Open();
                    await using var destStream = File.Open(destFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                    await stream.CopyToAsync(destStream);
                }

                _filesToCopy[bestReference.HashWithChildren] = Path.GetRelativePath(_context.VamDir, Path.Combine(destDir, Path.GetRelativePath(parentDir, bestReference.LocalPath))).NormalizePathSeparators();
            }

            return _filesToCopy[bestReference.HashWithChildren];
        }

        private async Task FixReferencesOneAsync(JsonFile json)
        {
            var jsonData = await ReadJson(json);
            foreach (var missing in json.Missing.OrderByDescending(t => t.Index))
            {
                var localReferencePath = missing.NormalizedLocalPath;
                var localReferenceFileName = Path.GetFileName(localReferencePath);

                FileReferenceBase bestReference = null;

                if (_filesByPath.Contains(localReferencePath))
                {
                    bestReference = _filesByPath[localReferencePath].Where(t => t.MissingChildren.Count == 0).OfType<FreeFile>().FirstOrDefault() ??  
                                    _filesByPath[localReferencePath].FirstOrDefault(t => t.MissingChildren.Count == 0) ??
                                    _filesByPath[localReferencePath].FirstOrDefault();
                }

                if (bestReference is null && 
                    json.IsVar && _varsByAuthor.TryGetValue(json.Var.Name.Author, out var varFiles) &&
                    varFiles.Contains(localReferenceFileName))
                {
                    var varFilesFromTheSameAuthor = varFiles[localReferenceFileName];
                    var localPaths = varFilesFromTheSameAuthor.Select(t => t.LocalPath);
                    var best = Process.ExtractOne(localReferencePath, localPaths);
                    bestReference = varFilesFromTheSameAuthor.ElementAt(best.Index);
                }

                if(bestReference is null && _filesByName.Contains(localReferenceFileName))
                {
                    var localReferences = _filesByName[localReferenceFileName];
                    var localPaths = localReferences.Select(t => t.LocalPath);
                    var best = Process.ExtractOne(localReferencePath, localPaths);
                    bestReference = localReferences.ElementAt(best.Index);
                }

                if (bestReference is null)
                    continue;

                var resolvedLocalPath = bestReference.LocalPath;
                if (bestReference is VarPackageFile varFile)
                    resolvedLocalPath = await CopyVarFile(varFile);

                ApplyFix(jsonData, missing, resolvedLocalPath, json.JsonPathInVar);
            }

            if(!_context.DryRun)
                await UpdateJson(json, jsonData);
        }

        private async Task UpdateJson(JsonFile json, IEnumerable<string> jsonData)
        {
            if (json.IsVar)
            {
                await using var varFileStream = File.Open(json.Var.FullPath, FileMode.Open, FileAccess.ReadWrite);
                using var varArchive = new ZipArchive(varFileStream, ZipArchiveMode.Update);
                var jsonEntry = varArchive.Entries.First(t => t.FullName.NormalizePathSeparators() == json.JsonPathInVar);
                jsonEntry.Delete();

                jsonEntry = varArchive.CreateEntry(json.JsonPathInVar, CompressionLevel.Fastest);
                var stream = jsonEntry.Open();
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                foreach (var s in jsonData)
                {
                    await writer.WriteLineAsync(s);
                }
            }
            else
            {
                await using var writer = new StreamWriter(json.Free.FullPath, false);
                foreach (var s in jsonData)
                {
                    await writer.WriteLineAsync(s);
                }
            }
        }

        private void ApplyFix(List<string> jsonData, Reference missing, string newReferenceLocalPath, string jsonLocalPath)
        {
            int sum = 0;
            for (var i = 0; i < jsonData.Count; i++)
            {
                sum += jsonData[i].Length;
                if (sum > missing.Index)
                {
                    int start = missing.Index - sum + jsonData[i].Length;
                    jsonData[i] = jsonData[i][..start] +  newReferenceLocalPath + jsonData[i][(start+missing.Length)..];
                    return;
                }
            }

            throw new InvalidOperationException($"Unable to find reference {missing.Value} in json file {jsonLocalPath}");
        }

        private async Task<List<string>> ReadJson(JsonFile json)
        {
            var sb = new List<string>();

            async Task ReadJsonInternal(StreamReader reader)
            {
                while (!reader.EndOfStream)
                {
                    sb.Add(await reader.ReadLineAsync());
                }
            }

            if (json.IsVar)
            {
                await using var varFileStream = File.OpenRead(json.Var.FullPath);
                using var varArchive = new ZipArchive(varFileStream);
                var jsonEntry = varArchive.Entries.First(t => t.FullName.NormalizePathSeparators() == json.JsonPathInVar);
                var stream = jsonEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                await ReadJsonInternal(reader);
            }
            else
            {
                using var reader = new StreamReader(json.Free.FullPath, Encoding.UTF8);
                await ReadJsonInternal(reader);
            }

            return sb;
        }

        private void InitLookups(IList<VarPackage> vars, IList<FreeFile> freeFiles)
        {
            var allFiles = vars
                .SelectMany(t => t.Files)
                .SelectMany(t => t.SelfAndChildren())
                .Cast<FileReferenceBase>()
                .Concat(freeFiles.SelectMany(t => t.SelfAndChildren()));

            _filesByName = allFiles.ToLookup(t => t.FilenameLower, StringComparer.InvariantCultureIgnoreCase);
            _filesByPath = allFiles.ToLookup(t => t.LocalPath, StringComparer.InvariantCultureIgnoreCase);


            _varsByAuthor = vars.GroupBy(t => t.Name.Author, StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(t => t.Key, t => t.SelectMany(x => x.Files).SelectMany(x => x.SelfAndChildren())
                    .ToLookup(x => x.FilenameLower, StringComparer.InvariantCultureIgnoreCase),
                StringComparer.InvariantCultureIgnoreCase);
        }
    }

    public interface IFixJsonDependenciesOperation : IOperation
    {
        Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, IList<JsonFile> jsonFiles, bool makeBackup);
    }
}
