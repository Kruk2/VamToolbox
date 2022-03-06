using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using MoreLinq;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;
using VamRepacker.Operations.Repo;

namespace VamRepacker.Operations.NotDestructive
{
    public class ScanJsonFilesOperation : IScanJsonFilesOperation
    {
        private readonly IProgressTracker _progressTracker;
        private readonly IFileSystem _fs;
        private readonly ILogger _logger;
        private readonly IJsonFileParser _jsonFileParser;
        private readonly ConcurrentBag<JsonFile> _jsonFiles = new();
        private readonly ConcurrentBag<string> _errors = new();
        private int _scanned;
        private int _total;

        private ILookup<string, FreeFile> _freeFilesIndex;
        private ILookup<string, VarPackage> _varFilesIndex;
        private ILookup<string, FileReferenceBase> _vamFilesById;
        private ILookup<string, FileReferenceBase> _morphFilesByName;

        private readonly ConcurrentDictionary<VarPackage, bool> _queuedVars = new();
        private readonly ConcurrentDictionary<FreeFile, bool> _queuedFreeFiles = new();
        private readonly ConcurrentBag<VarPackage> _queueVars = new();
        private readonly ConcurrentBag<FreeFile> _queueFree = new();

        private OperationContext _context;
        private readonly ConcurrentBag<(List<JsonReference> jsonReferences, Reference reference, IEnumerable<FileReferenceBase> matchedFiles, string uuidOrName)> _delayedReferencesToResolve = new();
        private readonly Dictionary<string, FileReferenceBase> _cachedDeleyedVam = new();
        private readonly Dictionary<string, FileReferenceBase> _cachedDeleyedMorphs = new();

        public ScanJsonFilesOperation(IProgressTracker progressTracker, IFileSystem fs, ILogger logger, IJsonFileParser jsonFileParser)
        {
            _progressTracker = progressTracker;
            _fs = fs;
            _logger = logger;
            _jsonFileParser = jsonFileParser;
        }

        public async Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles,
            IList<VarPackage> varFiles, IVarFilters varFilters = null)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            _context = context;
            _logger.Init("scan_json_files.log");
            _progressTracker.InitProgress();

            var potentialScenes = await InitLookups(varFiles, freeFiles, varFilters);

            foreach (var json in potentialScenes)
            {
                if (json.IsVar) _queuedVars[json.Var] = true;
                else _queuedFreeFiles[json.Free] = true;
            }

            _total = potentialScenes.Count;
            await RunScenesScan(potentialScenes);
            await RunDeepScan();

            _total = varFiles.Count + freeFiles.Count;
            await CalculateDeps(varFiles, freeFiles);

            var missingCount = _jsonFiles.Sum(s => s.Missing.Count);
            var resolvedCount = _jsonFiles.Sum(s => s.References.Count);
            _progressTracker.Complete($"Scanned {_scanned} json files for references. Found {missingCount} missing and {resolvedCount} resolved references.\r\nTook {stopWatch.Elapsed:hh\\:mm\\:ss}");

            var scenes = _jsonFiles.OrderBy(s => s.Name).ToList();
            PrintWarnings(scenes);

            return scenes;
        }

        private async Task<List<PotentialJsonFile>> InitLookups(IList<VarPackage> varFiles, IList<FreeFile> freeFiles, IVarFilters varFilters)
        {
            return await Task.Run(() =>
            {
                var varFilesWithScene = varFiles
                    .Where(t => (varFilters != null && varFilters.Matches(t.FullPath)) || varFilters is null)
                    .Where(t => t.Files.SelectMany(x => x.SelfAndChildren())
                        .Any(x => x.FilenameLower != "meta.json" && x.ExtLower is ".json" or ".vap" or ".vaj"));

                _freeFilesIndex = freeFiles
                    .ToLookup(f => f.LocalPath, f => f, StringComparer.InvariantCultureIgnoreCase);
                _varFilesIndex = varFiles.ToLookup(t => t.Name.PackageNameWithoutVersion, StringComparer.InvariantCultureIgnoreCase);
                InitVamFilesById(freeFiles, varFiles);
                InitMorphNames(freeFiles);

                return freeFiles
                    .Where(_ => varFilters is null)
                    .SelectMany(x => x.SelfAndChildren())
                    .Where(t => t.ExtLower is ".json" or ".vap" or ".vaj")
                    .Select(t => new PotentialJsonFile(t))
                    .Concat(varFilesWithScene.Select(t => new PotentialJsonFile(t)))
                    .ToList();
            });
        }

        private async Task CalculateDeps(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
        {
            var dependencies = varFiles.Cast<IVamObjectWithDependencies>().Concat(freeFiles).ToList();
            dependencies.ForEach(t => t.ClearDependencies());
            var progress = 0;
            var depScanBlock = new ActionBlock<IVamObjectWithDependencies>(t =>
                {
                    t.CalculateDeps();
                    t.CalculateShallowDeps();
                    _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref progress), _total, $"Calculating dependencies for {t}"));
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _context.Threads
                });

            foreach (var d in dependencies)
                depScanBlock.Post(d);

            depScanBlock.Complete();
            await depScanBlock.Completion;
        }

        private void InitVamFilesById(IList<FreeFile> freeFiles, IList<VarPackage> varFiles)
        {
            var vamFilesFromVars = varFiles
                .SelectMany(t => t.Files.Where(x => x.InternalId != null));

            _vamFilesById = vamFilesFromVars.Cast<FileReferenceBase>()
                .Concat(freeFiles.Where(t => t.InternalId != null))
                .Where(t => KnownNames.HairClothDirs.Any(x => t.LocalPath.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                .ToLookup(t => t.InternalId);
        }

        private void InitMorphNames(IEnumerable<FreeFile> freeFiles) => _morphFilesByName = freeFiles
            .Where(t => t.MorphName != null && KnownNames.MorphDirs.Any(x => t.LocalPath.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            .Cast<FileReferenceBase>()
            .ToLookup(t => t.MorphName);

        private async Task RunDeepScan()
        {
            do
            {
                while (_queueFree.Count > 0 || _queueVars.Count > 0)
                {
                    var json = _queueVars.Distinct()
                        .Select(t => new PotentialJsonFile(t))
                        .Concat(_queueFree.Distinct().Select(t => new PotentialJsonFile(t)))
                        .ToList();

                    _total += json.Count;
                    _queueFree.Clear();
                    _queueVars.Clear();
                    await RunScenesScan(json);
                }
            } while (await ResolveDelayedReferences());
        }

        private async Task RunScenesScan(IEnumerable<PotentialJsonFile> potentialScenes)
        {
            var scanSceneBlock = new ActionBlock<PotentialJsonFile>(
                ScanJsonAsync,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _context.Threads
                });

            foreach (var potentialScene in potentialScenes)
            {
                scanSceneBlock.Post(potentialScene);
            }

            scanSceneBlock.Complete();
            await scanSceneBlock.Completion;
        }

        private static string MigrateLegacyPaths(string refPath)
        {
            if (refPath.StartsWith(@"Saves\Scripts\", StringComparison.OrdinalIgnoreCase)) return @"Custom\Scripts\" + refPath.Substring(@"Saves\Scripts\".Length);
            if (refPath.StartsWith(@"Saves\Assets\", StringComparison.OrdinalIgnoreCase)) return @"Custom\Assets\" + refPath.Substring(@"Saves\Assets\".Length);
            if (refPath.StartsWith(@"Import\morphs\", StringComparison.OrdinalIgnoreCase)) return @"Custom\Atom\Person\Morphs\" + refPath.Substring(@"Import\morphs\".Length);
            if (refPath.StartsWith(@"Textures\", StringComparison.OrdinalIgnoreCase)) return @"Custom\Atom\Person\Textures\" + refPath.Substring(@"Textures\".Length);
            return refPath;
        }

        private void PrintWarnings(List<JsonFile> scenes)
        {
            var uniqueMissingVars = scenes.SelectMany(t => t.IsVar
                    ? t.Var.JsonFiles.SelectMany(x => x.Missing).Select(x => (JsonFile: t, x.EstimatedVarName))
                        .Where(x => x.EstimatedVarName != null)
                    : t.Free.JsonFiles.SelectMany(x => x.Missing).Select(x => (JsonFile: t, x.EstimatedVarName))
                        .Where(x => x.EstimatedVarName != null))
                .GroupBy(t => t.EstimatedVarName)
                .Select(t =>
                {
                    VarPackageName.TryGet(t.Key + ".var", out var x);
                    return (t.Select(y => y.JsonFile.Name).Distinct().ToList(), VarName: x, t.Key);
                });

            _logger.Log("Missing vars:");
            foreach (var (jsonFiles, varName, key) in uniqueMissingVars.OrderBy(t => t.VarName?.Filename ?? string.Empty))
            {
                if (varName is null)
                {
                    _logger.Log($"Unable to parse: {key} from " + string.Join(" AND ", jsonFiles));
                    continue;
                }
                if (!_varFilesIndex.Contains(varName.PackageNameWithoutVersion))
                {
                    _logger.Log(varName.Filename + " from " + string.Join(" AND ", jsonFiles));
                    continue;
                }
                if (varName.Version == -1)
                    continue;

                VarPackage matchingVar;
                if (varName.MinVersion)
                {
                    matchingVar = MoreEnumerable.MaxBy(_varFilesIndex[varName.PackageNameWithoutVersion]
                            .Where(t => t.Name.Version >= varName.Version), t => t.Name.Version).First();
                }
                else
                {
                    matchingVar = _varFilesIndex[varName.PackageNameWithoutVersion]
                        .FirstOrDefault(t => t.Name.Version == varName.Version);
                }

                if (matchingVar is null)
                    _logger.Log(varName.Filename + " from " + string.Join(" AND ", jsonFiles));
            }

            _logger.Log("");
            var errors = scenes.SelectMany(t => t.IsVar ? t.Var.UnresolvedDependencies : t.Free.UnresolvedDependencies).Distinct();
            foreach (var error in errors)
            {
                _logger.Log(error);
            }
        }

        private async Task ScanJsonAsync(PotentialJsonFile potentialJson)
        {
            try 
            { 
                _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _total, potentialJson.Name));
                foreach (var (jsonStream, jsonLocalPath) in potentialJson.OpenJsons())
                    await ScanJsonAsync(jsonStream, jsonLocalPath, potentialJson);

            }
            catch(Exception ex)
            {
                _errors.Add($"[UNKNOWN-ERROR] Unable to process {potentialJson.Name} because: {ex}");
            }
            finally
            {
                potentialJson.Dispose();
            }
        }

        private async Task ScanJsonAsync(Stream potentialSceneStream, string jsonLocalPath,
            PotentialJsonFile potentialJson)
        {
            using var streamReader = new StreamReader(potentialSceneStream);
            var sceneFolder =  potentialJson.Free?.LocalPath == null ? null : _fs.Path.GetDirectoryName(potentialJson.Free.LocalPath);
            Reference nextScanForUuidOrMorphName = null;  
            var references = new List<JsonReference>();
            var missing = new HashSet<Reference>();
            var offset = 0;
            var jsonDirectoryPath = Path.GetDirectoryName(jsonLocalPath).NormalizePathSeparators();
            var hasDelayedReferences = false;

            while(!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    continue;

                if (nextScanForUuidOrMorphName != null)
                {
                    if (line.Contains("\"internalId\""))
                    {
                        var internalId = line.Replace("\"internalId\"", "");
                        internalId = internalId[(internalId.IndexOf("\"") + 1)..internalId.LastIndexOf("\"")];
                        var (jsonReferenceById, delayedReference) = MatchVamJsonReferenceById(references, internalId, nextScanForUuidOrMorphName);
                        if (jsonReferenceById != null)
                            references.Add(jsonReferenceById);
                        else if (!delayedReference)
                            missing.Add(nextScanForUuidOrMorphName);
                        else
                            hasDelayedReferences = true;

                        nextScanForUuidOrMorphName = null;
                        offset += line.Length;
                        continue;
                    }

                    if (line.Contains("\"name\""))
                    {
                        var morphName = line.Replace("\"name\"", "");
                        morphName = morphName[(morphName.IndexOf("\"") + 1)..morphName.LastIndexOf("\"")];
                        var (jsonReferenceByMorphName, delayedReference) = MatchMorphJsonReferenceByName(references, morphName, nextScanForUuidOrMorphName);
                        if (jsonReferenceByMorphName != null)
                            references.Add(jsonReferenceByMorphName);
                        else if (!delayedReference)
                            missing.Add(nextScanForUuidOrMorphName);
                        else
                            hasDelayedReferences = true;

                        nextScanForUuidOrMorphName = null;
                        offset += line.Length;
                        continue;
                    }

                    nextScanForUuidOrMorphName = null;
                }

                Reference reference;
                string referenceParseError;
                try
                {
                    reference = _jsonFileParser.GetAsset(line, offset, out referenceParseError);
                }
                catch (Exception e)
                {
                    _logger.Log($"[ERROR] {e.Message} Unable to parse asset '{line}' in {jsonLocalPath}. {(potentialJson.IsVar ? "Var: " + potentialJson.Var.Name.Filename : "")}.");
                    throw;
                }
                finally
                {
                    offset += line.Length;
                }

                if (reference is null)
                {
                    if(referenceParseError != null)
                        _errors.Add(referenceParseError);
                    continue;
                }

                JsonReference jsonReference = null;
                if (reference.Value.Contains(":"))
                {
                    jsonReference = ScanPackageSceneReference(potentialJson, reference, reference.Value, jsonDirectoryPath);
                }
                if(jsonReference == null && (!reference.Value.Contains(':') || (reference.Value.Contains(':') && reference.Value.StartsWith("SELF:"))))
                {
                    jsonReference = ScanFreeFileSceneReference(sceneFolder, reference);
                    // it can be inside scene in var
                    if (jsonReference == default && potentialJson.IsVar)
                    {
                        jsonReference = ScanPackageSceneReference(potentialJson, reference, "SELF:" + reference.Value, jsonDirectoryPath);
                    }
                }

                if (jsonReference != null)
                {
                    references.Add(jsonReference);
                }
                else
                {
                    if (reference.Value.EndsWith(".vam", StringComparison.OrdinalIgnoreCase))
                        nextScanForUuidOrMorphName = reference;
                    if (reference.Value.EndsWith(".vmi", StringComparison.OrdinalIgnoreCase))
                        nextScanForUuidOrMorphName = reference;
                    else
                        missing.Add(reference);
                }
            }

            var item = new JsonFile(potentialJson, jsonLocalPath, references, missing.ToList());
            if (references.Count > 0 || missing.Count > 0 || hasDelayedReferences)
                _jsonFiles.Add(item);

            QueueReferences(references);
        }

        private (JsonReference, bool) MatchVamJsonReferenceById(List<JsonReference> jsonReferences, string internalId, Reference reference)
        {
            return MatchAssetByUuidOrName(jsonReferences, internalId, reference, _vamFilesById);
        }

        private (JsonReference, bool) MatchMorphJsonReferenceByName(List<JsonReference> jsonReferences, string displayName, Reference reference)
        {
            return MatchAssetByUuidOrName(jsonReferences, displayName, reference, _morphFilesByName);
        }

        private (JsonReference, bool) MatchAssetByUuidOrName(List<JsonReference> jsonReferences, string uuidOrName, Reference reference, ILookup<string, FileReferenceBase> lookup)
        {
            if (string.IsNullOrWhiteSpace(uuidOrName))
                throw new VamRepackerException("Invalid displayNameOrUuid");

            var matchedAssets = lookup[uuidOrName];
            if (!matchedAssets.Any())
                return (null, false);

            if (matchedAssets.Count() == 1)
                return (new JsonReference(matchedAssets.First(), reference), false);

            _delayedReferencesToResolve.Add((jsonReferences, reference, matchedAssets, uuidOrName));

            return (null, true);
        }

        private Task<bool> ResolveDelayedReferences()
        {
            return Task.Run(() =>
            {
                if (_delayedReferencesToResolve.Count == 0)
                    return false;

                var createdReferences = new List<JsonReference>(_delayedReferencesToResolve.Count);
                foreach (var (jsonReferences, reference, matchedAssets, uuidOrName) in _delayedReferencesToResolve)
                {
                    var isVam = reference.Value.EndsWith(".vam", StringComparison.OrdinalIgnoreCase);

                    void AddJsonReference(JsonReference referenceToAdd)
                    {
                        jsonReferences.Add(referenceToAdd);
                        var jsonFile = _jsonFiles.First(t => ReferenceEquals(t.References, jsonReferences));
                        referenceToAdd.FromJson = jsonFile;
                        if (referenceToAdd.IsVarReference) jsonFile.VarReferences.Add(referenceToAdd.VarFile.ParentVar);
                        else jsonFile.FreeReferences.Add(referenceToAdd.File);
                    }

                    void AddReference(FileReferenceBase fileToAdd)
                    {
                        var referenceToAdd = new JsonReference(fileToAdd, reference);
                        AddJsonReference(referenceToAdd);
                    }

                    if (isVam && _cachedDeleyedVam.TryGetValue(uuidOrName, out var cachedReference))
                    {
                        AddReference(cachedReference);
                        continue;
                    }

                    if (!isVam && _cachedDeleyedMorphs.TryGetValue(uuidOrName, out var cachedMorphReference))
                    {
                        AddReference(cachedMorphReference);
                        continue;
                    }

                    // prefer files that are most used
                    IEnumerable<FileReferenceBase> bestMatchesByJsonCount =
                        MoreLinq.MoreEnumerable.MaxBy(matchedAssets, t => t.JsonReferences.Count);
                    if (bestMatchesByJsonCount.Count() == 1)
                    {
                        AddReference(bestMatchesByJsonCount.First());
                        continue;
                    }

                    // prefer files that are in vam dir, if any
                    bool IsInVamDirExpression(FileReferenceBase t) => (t is VarPackageFile varFile && varFile.ParentVar.IsInVaMDir) || (t is FreeFile freeFile && freeFile.IsInVaMDir);
                    if (bestMatchesByJsonCount.Any(IsInVamDirExpression))
                    {
                        bestMatchesByJsonCount = bestMatchesByJsonCount.Where(IsInVamDirExpression);
                    }

                    // prefer vars/json with least dependencies
                    var objectsWithDependencies = bestMatchesByJsonCount
                        .Select(t => t is VarPackageFile varFile ? varFile.ParentVar : (IVamObjectWithDependencies)t)
                        .ToList();

                    objectsWithDependencies.ForEach(t => t.CalculateDeps());

                    var dependenciesCount = objectsWithDependencies.Select(t =>
                        t.AllResolvedVarDependencies.Count + t.AllResolvedFreeDependencies.Count);
                    var minCount = dependenciesCount.Min();
                    var zipped = bestMatchesByJsonCount.Zip(dependenciesCount);

                    var bestMatches = zipped.Where(t => t.Second == minCount).Select(t => t.First);
                    FileReferenceBase bestMatch;

                    if (bestMatches.Count() == 1)
                    {
                        bestMatch = bestMatches.First();
                    }
                    else
                    {
                        var byMostUsedFile = MoreLinq.MoreEnumerable.MaxBy(bestMatches, t => t.JsonReferences.Count);
                        var bySmallestSize = MoreLinq.MoreEnumerable.MinBy(byMostUsedFile,
                            t => t is VarPackageFile varFile ? varFile.ParentVar.Size : ((FreeFile)t).SizeWithChildren);
                        bestMatch = bySmallestSize.OrderBy(t =>
                            t is VarPackageFile varFile ? varFile.ParentVar.FullPath : ((FreeFile)t).FullPath).First();
                    }

                    AddReference(bestMatch);

                    if (isVam)
                        _cachedDeleyedVam[uuidOrName] = bestMatch;
                    else
                        _cachedDeleyedMorphs[uuidOrName] = bestMatch;
                }

                QueueReferences(createdReferences);
                _delayedReferencesToResolve.Clear();
                return true;
            });
        }

        private void QueueReferences(IReadOnlyCollection<JsonReference> references)
        {
            var referencedVars = references
                .Where(t => t.IsVarReference && KnownNames.ExtReferencesToPresets.Contains(t.VarFile.ExtLower))
                .Select(t => t.VarFile.ParentVar);
            var referencedFreeFiles = references
                .Where(t => !t.IsVarReference && KnownNames.ExtReferencesToPresets.Contains(t.File.ExtLower))
                .Select(t => t.File);

            foreach (var referencedVar in referencedVars)
            {
                if (_queuedVars.TryAdd(referencedVar, true))
                {
                    _queueVars.Add(referencedVar);
                    referencedVar.ClearDependencies();
                }
            }

            foreach (var referencedFreeFile in referencedFreeFiles)
            {
                if (_queuedFreeFiles.TryAdd(referencedFreeFile, true))
                {
                    _queueFree.Add(referencedFreeFile);
                    referencedFreeFile.ClearDependencies();
                }
            }
        }

        private JsonReference ScanFreeFileSceneReference(string sceneFolder, Reference reference)
        {
            if (reference.Value.Contains(':') && !reference.Value.StartsWith("SELF:"))
                throw new VamRepackerException($"{sceneFolder} refers to var but processing free file reference");

            var refPath = reference.Value.Split(':').Last();
            refPath = refPath.NormalizePathSeparators();
            refPath = MigrateLegacyPaths(refPath);
            if (sceneFolder != null && _freeFilesIndex[_fs.Path.Combine(sceneFolder, refPath).NormalizePathSeparators()] is var f1 && f1.Any())
            {
                var x = f1.FirstOrDefault(t => t.IsInVaMDir) ?? f1.FirstOrDefault();
                return new JsonReference(x, reference);
            }
            if (_freeFilesIndex[refPath] is var f2 && f2.Any())
            {
                var x = f2.FirstOrDefault(t => t.IsInVaMDir) ?? f2.FirstOrDefault();
                return new JsonReference(x, reference);
            }

            return default;
        }

        private JsonReference ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, string refPath, string sceneJsonPath)
        {
            var refPathSplit = refPath.Split(':');
            var assetName = refPathSplit[1];

            VarPackage varToSearch = null;
            if (refPathSplit[0] == "SELF")
            {
                if (!potentialJson.IsVar)
                    return default;

                varToSearch = potentialJson.Var;
            }
            else
            {
                if (!VarPackageName.TryGet(refPathSplit[0] + ".var", out var varFile))
                {
                    _errors.Add($"[INTERNAL-ERROR] {refPath} was neither a SELF reference or VAR in {potentialJson}");
                    return default;
                }

                if (!_varFilesIndex.Contains(varFile.PackageNameWithoutVersion))
                {
                    return default;
                }

                if (varFile.Version == -1)
                {
                    varToSearch = MoreEnumerable.MaxBy(_varFilesIndex[varFile.PackageNameWithoutVersion], t => t.Name.Version).First();
                }
                else if (varFile.MinVersion)
                {
                    varToSearch = MoreEnumerable.MaxBy(_varFilesIndex[varFile.PackageNameWithoutVersion]
                            .Where(t => t.Name.Version >= varFile.Version), t => t.Name.Version).First();
                }
                else
                {
                    varToSearch = _varFilesIndex[varFile.PackageNameWithoutVersion]
                        .FirstOrDefault(t => t.Name.Version == varFile.Version);
                }
            }

            if (varToSearch != null)
            {
                var varAssets = varToSearch.FilesDict;
                var refInScene = Path.GetFullPath(MigrateLegacyPaths(Path.Combine(sceneJsonPath, assetName)), "C:/").Replace("C:\\", string.Empty).NormalizePathSeparators();
                assetName = assetName.NormalizePathSeparators();
                assetName = MigrateLegacyPaths(assetName);

                if (varAssets.TryGetValue(refInScene, out var f1))
                {
                    //_logger.Log($"[RESOLVER] Found f1 {f1.ParentVar.Name.Filename} for reference {refer}")}");ence.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath
                    return new JsonReference(f1, reference);
                }

                if (varAssets.TryGetValue(assetName, out var f2))
                {
                    //_logger.Log($"[RESOLVER] Found f2 {f2.ParentVar.Name.Filename} for reference {reference.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath}")}");
                    return new JsonReference(f2, reference);
                }
            }

            return null;
        }
    }

    public interface IScanJsonFilesOperation : IOperation
    {
        Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles,
            IList<VarPackage> varFiles, IVarFilters varFilters = null);
    }
}
