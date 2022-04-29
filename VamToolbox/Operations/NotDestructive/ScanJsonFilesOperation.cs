using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using MoreLinq;
using VamToolbox.FilesGrouper;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.NotDestructive;

public sealed class ScanJsonFilesOperation : IScanJsonFilesOperation
{
    private readonly IProgressTracker _progressTracker;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private readonly IJsonFileParser _jsonFileParser;
    private readonly IReferenceCacheReader _referenceCacheReader;
    private readonly IUuidReferenceResolver _uuidReferenceResolver;
    private readonly ConcurrentBag<JsonFile> _jsonFiles = new();
    private readonly ConcurrentBag<string> _errors = new();
    private int _scanned;
    private int _total;

    private ILookup<string, FreeFile> _freeFilesIndex = null!;
    private ILookup<string, VarPackage> _varFilesIndex = null!;
    private OperationContext _context = null!;

    public ScanJsonFilesOperation(
        IProgressTracker progressTracker, 
        IFileSystem fs, 
        ILogger logger, 
        IJsonFileParser jsonFileParser, 
        IReferenceCacheReader referenceCacheReader,
        IUuidReferenceResolver uuidReferenceResolver)
    {
        _progressTracker = progressTracker;
        _fs = fs;
        _logger = logger;
        _jsonFileParser = jsonFileParser;
        _referenceCacheReader = referenceCacheReader;
        _uuidReferenceResolver = uuidReferenceResolver;
    }

    public async Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles, IList<VarPackage> varFiles)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        _context = context;
        await _logger.Init("scan_json_files.log");
        _progressTracker.InitProgress("Scanning scenes/presets references");

        var potentialScenes = await InitLookups(varFiles, freeFiles);
        await _referenceCacheReader.ReadCache(potentialScenes);

        _total = potentialScenes.Count;
        await RunScenesScan(potentialScenes);

        _total = varFiles.Count + freeFiles.Count;
        await Task.Run(async () => await CalculateDeps(varFiles, freeFiles));
        await _referenceCacheReader.SaveCache(varFiles, freeFiles);

        var missingCount = _jsonFiles.Sum(s => s.Missing.Count);
        var resolvedCount = _jsonFiles.Sum(s => s.References.Count);
        var scenes = _jsonFiles.OrderBy(s => s.ToString()).ToList();
        await Task.Run(() => PrintWarnings(scenes));

        _progressTracker.Complete($"Scanned {_scanned} json files for references. Found {missingCount} missing and {resolvedCount} resolved references.\r\nTook {stopWatch.Elapsed:hh\\:mm\\:ss}");
        return scenes;
    }

    private async Task<List<PotentialJsonFile>> InitLookups(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        await _uuidReferenceResolver.InitLookups(freeFiles, varFiles);

        return await Task.Run(() =>
        {
            var varFilesWithScene = varFiles
                .Where(t => t.Files.SelectMany(x => x.SelfAndChildren())
                    .Any(x => x.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(x.ExtLower)));

            _freeFilesIndex = freeFiles
                .ToLookup(f => f.LocalPath, f => f, StringComparer.InvariantCultureIgnoreCase);
            _varFilesIndex = varFiles.ToLookup(t => t.Name.PackageNameWithoutVersion, StringComparer.InvariantCultureIgnoreCase);
            

            return freeFiles
                .SelectMany(x => x.SelfAndChildren())
                .Where(t => KnownNames.IsPotentialJsonFile(t.ExtLower))
                .Select(t => new PotentialJsonFile(t))
                .Concat(varFilesWithScene.Select(t => new PotentialJsonFile(t)))
                .ToList();
        });
    }

    private async Task CalculateDeps(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        _progressTracker.Report("Calculating dependencies", forceShow: true);

        var dependencies = varFiles.Cast<IVamObjectWithDependencies>().Concat(freeFiles).ToList();
        dependencies.ForEach(t => t.ClearDependencies());

        var depScanBlock = new ActionBlock<IVamObjectWithDependencies>(t =>
            {
                _ = _context.ShallowDeps ? t.TrimmedResolvedVarDependencies : t.AllResolvedVarDependencies;
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

        await _uuidReferenceResolver.ResolveDelayedReferences();
    }

    private static string MigrateLegacyPaths(string refPath)
    {
        if (refPath.StartsWith(@"Saves\Scripts\", StringComparison.OrdinalIgnoreCase)) return string.Concat(@"Custom\Scripts\", refPath.AsSpan(@"Saves\Scripts\".Length));
        if (refPath.StartsWith(@"Saves\Assets\", StringComparison.OrdinalIgnoreCase)) return string.Concat(@"Custom\Assets\", refPath.AsSpan(@"Saves\Assets\".Length));
        if (refPath.StartsWith(@"Import\morphs\", StringComparison.OrdinalIgnoreCase)) return string.Concat(@"Custom\Atom\Person\Morphs\", refPath.AsSpan(@"Import\morphs\".Length));
        if (refPath.StartsWith(@"Textures\", StringComparison.OrdinalIgnoreCase)) return string.Concat(@"Custom\Atom\Person\Textures\", refPath.AsSpan(@"Textures\".Length));
        return refPath;
    }

    private void PrintWarnings(List<JsonFile> scenes)
    {
        _progressTracker.Report("Saving logs", forceShow: true);

        var uniqueMissingVars = scenes.SelectMany(t => t.Missing)
            .GroupBy(t => t.EstimatedVarName)
            .Select(t =>
            {
                VarPackageName.TryGet(t.Key + ".var", out var x);
                return (fromJsonFiles: t.Select(y => y.FromJsonFile).Distinct().ToList(), VarName: x, t.Key);
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

            VarPackage? matchingVar;
            if (varName.MinVersion)
            {
                matchingVar = MoreEnumerable.MaxBy(_varFilesIndex[varName.PackageNameWithoutVersion]
                    .Where(t => t.Name.Version >= varName.Version), t => t.Name.Version)
                    .First();
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
        var errors = scenes.SelectMany(t => t.File.IsVar ? t.File.Var.UnresolvedDependencies : t.File.Free.UnresolvedDependencies).Distinct();
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
            foreach (var openedJson in potentialJson.OpenJsons())
                await ScanJsonAsync(openedJson, potentialJson);

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

    private async Task ScanJsonAsync(OpenedPotentialJson openedJson, PotentialJsonFile potentialJson)
    {
        using var streamReader = openedJson.Stream == null ? null : new StreamReader(openedJson.Stream);
        string? localSavesFolder = _fs.Path.GetDirectoryName(openedJson.File.LocalPath).NormalizePathSeparators();
        if (!localSavesFolder.StartsWith("Saves", StringComparison.OrdinalIgnoreCase))
            localSavesFolder = null;

        Reference? nextScanForUuidOrMorphName = null;
        JsonReference? resolvedReferenceWhenUuidMatchingFails = null;

        var jsonFile = new JsonFile(openedJson);
        var offset = 0;
        var hasDelayedReferences = false;

        if (openedJson.CachedReferences != null)
        {
            foreach (var reference in openedJson.CachedReferences)
            {
                #if DEBUG
                if(openedJson.File.LocalPath.EndsWith("anime dream/anime dream.json", StringComparison.Ordinal) && reference.Value.Contains("Custom/Hair/Female/Supa/Tron/Tron Bun.vam"))
                    Debug.Write(true);
                #endif
                (nextScanForUuidOrMorphName, resolvedReferenceWhenUuidMatchingFails) = ProcessJsonReference(reference);

                if (reference.InternalId != null)
                {
                    if(nextScanForUuidOrMorphName is null) throw new ArgumentException("Uuid reference is null but got internal id");
                    ProcessVamReference(reference);
                }
                else if (reference.MorphName != null)
                {
                    if (nextScanForUuidOrMorphName is null) throw new ArgumentException("morph reference is null but got morph name");
                    ProcessMorphReference(reference);
                }
            }
        }

        while(streamReader is { EndOfStream: false })
        {
            var line = await streamReader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
                continue;

            if (nextScanForUuidOrMorphName != null)
            {
                if (line.Contains("\"internalId\""))
                {
                    var internalId = line.Replace("\"internalId\"", "");
                    nextScanForUuidOrMorphName.InternalId = internalId[(internalId.IndexOf('\"') + 1)..internalId.LastIndexOf('\"')];
                    ProcessVamReference(nextScanForUuidOrMorphName);

                    nextScanForUuidOrMorphName = null;
                    resolvedReferenceWhenUuidMatchingFails = null;
                    offset += line.Length;
                    continue;
                }

                if (line.Contains("\"name\""))
                {
                    var morphName = line.Replace("\"name\"", "");
                    nextScanForUuidOrMorphName.MorphName = morphName[(morphName.IndexOf('\"') + 1)..morphName.LastIndexOf('\"')];
                    ProcessMorphReference(nextScanForUuidOrMorphName);

                    nextScanForUuidOrMorphName = null;
                    resolvedReferenceWhenUuidMatchingFails = null;
                    offset += line.Length;
                    continue;
                }

                if (resolvedReferenceWhenUuidMatchingFails != null)
                {
                    jsonFile.AddReference(resolvedReferenceWhenUuidMatchingFails);
                    resolvedReferenceWhenUuidMatchingFails = null;
                }
                else
                {
                    jsonFile.AddMissingReference(nextScanForUuidOrMorphName);
                }

                nextScanForUuidOrMorphName = null;
            }

            Reference? reference;
            string? referenceParseError;
            try
            {
                reference = _jsonFileParser.GetAsset(line, offset, openedJson.File, out referenceParseError);
            }
            catch (Exception e)
            {
                _logger.Log($"[ERROR] {e.Message} Unable to parse asset '{line}' in {openedJson.File}");
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

            (nextScanForUuidOrMorphName, resolvedReferenceWhenUuidMatchingFails) = ProcessJsonReference(reference);
        }

        if (jsonFile.References.Count > 0 || jsonFile.Missing.Count > 0 || hasDelayedReferences)
        {
            _jsonFiles.Add(jsonFile);
            openedJson.File.JsonFile = jsonFile;
        }

        (Reference? nextScanForUuidOrMorphName, JsonReference? jsonReference) ProcessJsonReference(Reference reference)
        {
            JsonReference? jsonReference = null;
            if (reference.Value.Contains(':'))
            {
                jsonReference = ScanPackageSceneReference(potentialJson, reference, reference.Value, localSavesFolder);
            }

            if (jsonReference == null && (!reference.Value.Contains(':') ||
                                          (reference.Value.Contains(':') && reference.Value.StartsWith("SELF:", StringComparison.Ordinal))))
            {
                jsonReference = ScanFreeFileSceneReference(localSavesFolder, reference);
                // it can be inside scene in var
                if (jsonReference == default && potentialJson.IsVar)
                {
                    jsonReference = ScanPackageSceneReference(potentialJson, reference, "SELF:" + reference.Value, localSavesFolder);
                }
            }

            if (reference.Value.EndsWith(".vam", StringComparison.OrdinalIgnoreCase))
                nextScanForUuidOrMorphName = reference;
            else if (reference.Value.EndsWith(".vmi", StringComparison.OrdinalIgnoreCase))
                nextScanForUuidOrMorphName = reference;
            else if (jsonReference != null)
                jsonFile.AddReference(jsonReference);
            else
                jsonFile.AddMissingReference(reference);

            return (nextScanForUuidOrMorphName, jsonReference);
        }

        void ProcessMorphReference(Reference morphReference)
        {
            var (jsonReferenceByMorphName, delayedReference) = _uuidReferenceResolver.MatchMorphJsonReferenceByName(jsonFile, morphReference, potentialJson.Var, resolvedReferenceWhenUuidMatchingFails?.File);
            if (jsonReferenceByMorphName != null)
                jsonFile.AddReference(jsonReferenceByMorphName);
            else if (!delayedReference)
                jsonFile.AddMissingReference(morphReference);
            else
                hasDelayedReferences = true;
        }

        void ProcessVamReference(Reference vamReference)
        {
            var (jsonReferenceById, delayedReference) = _uuidReferenceResolver.MatchVamJsonReferenceById(jsonFile, vamReference, potentialJson.Var, resolvedReferenceWhenUuidMatchingFails?.File);
            if (jsonReferenceById != null)
                jsonFile.AddReference(jsonReferenceById);
            else if (!delayedReference)
                jsonFile.AddMissingReference(vamReference);
            else
                hasDelayedReferences = true;
        }
    }

    private JsonReference? ScanFreeFileSceneReference(string? localSceneFolder, Reference reference)
    {
        if (reference.Value.Contains(':') && !reference.Value.StartsWith("SELF:", StringComparison.Ordinal))
            throw new VamToolboxException($"{reference.FromJsonFile} {reference.Value} refers to var but processing free file reference");

        var refPath = reference.Value.Split(':').Last();
        refPath = refPath.NormalizeAssetPath();
        refPath = MigrateLegacyPaths(refPath);
        // searching in localSceneFolder for var json files is handled in ScanPackageSceneReference
        if (!reference.FromJsonFile.IsVar && localSceneFolder is not null && _freeFilesIndex[_fs.Path.Combine(localSceneFolder, refPath).NormalizePathSeparators()] is var f1 && f1.Any())
        {
            f1 = f1.OrderByDescending(t => t.UsedByVarPackagesOrFreeFilesCount).ThenBy(t => t.FullPath);
            var x = f1.FirstOrDefault(t => t.IsInVaMDir) ?? f1.First();
            return new JsonReference(x, reference);
        }
        if (_freeFilesIndex[refPath] is var f2 && f2.Any())
        {
            f2 = f2.OrderByDescending(t => t.UsedByVarPackagesOrFreeFilesCount).ThenBy(t => t.FullPath);
            var x = f2.FirstOrDefault(t => t.IsInVaMDir) ?? f2.First();
            return new JsonReference(x, reference);
        }

        return default;
    }

    private JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, string refPath, string? localSceneFolder)
    {
        var refPathSplit = refPath.Split(':');
        var assetName = refPathSplit[1];

        VarPackage? varToSearch = null;
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
            assetName = assetName.NormalizeAssetPath();
            assetName = MigrateLegacyPaths(assetName);

            if (potentialJson.Var == varToSearch && localSceneFolder is not null)
            {
                var refInScene = _fs.Path.Combine(localSceneFolder, assetName).NormalizePathSeparators();
                if (varAssets.TryGetValue(refInScene, out var f1))
                {
                    //_logger.Log($"[RESOLVER] Found f1 {f1.ParentVar.Name.Filename} for reference {refer}")}");ence.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath
                    return new JsonReference(f1, reference);
                }
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
    Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles, IList<VarPackage> varFiles);
}