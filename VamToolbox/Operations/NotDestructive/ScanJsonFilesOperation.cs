using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
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
    private readonly IReferencesResolver _referencesResolver;
    private readonly ConcurrentBag<JsonFile> _jsonFiles = new();
    private readonly ConcurrentBag<string> _errors = new();
    private int _scanned;
    private int _total;

    private OperationContext _context = null!;

    public ScanJsonFilesOperation(
        IProgressTracker progressTracker,
        IFileSystem fs,
        ILogger logger,
        IJsonFileParser jsonFileParser,
        IReferenceCacheReader referenceCacheReader,
        IUuidReferenceResolver uuidReferenceResolver,
        IReferencesResolver referencesResolver)
    {
        _progressTracker = progressTracker;
        _fs = fs;
        _logger = logger;
        _jsonFileParser = jsonFileParser;
        _referenceCacheReader = referenceCacheReader;
        _uuidReferenceResolver = uuidReferenceResolver;
        _referencesResolver = referencesResolver;
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
        await Task.Run(() => PrintWarnings(scenes, varFiles));

        _progressTracker.Complete($"Scanned {_scanned} json files for references. Found {missingCount} missing and {resolvedCount} resolved references.\r\nTook {stopWatch.Elapsed:hh\\:mm\\:ss}");
        return scenes;
    }

    private async Task<List<PotentialJsonFile>> InitLookups(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        await _uuidReferenceResolver.InitLookups(freeFiles, varFiles);
        await _referencesResolver.InitLookups(freeFiles, varFiles, _errors);

        return await Task.Run(() => {

            var varFilesWithScene = varFiles
                .Where(t => t.Files.SelectMany(x => x.SelfAndChildren())
                    .Any(x => x.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(x.ExtLower)));

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

        var depScanBlock = new ActionBlock<IVamObjectWithDependencies>(t => {
            _ = _context.ShallowDeps ? t.TrimmedResolvedVarDependencies : t.AllResolvedVarDependencies;
        },
            new ExecutionDataflowBlockOptions {
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
            new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = _context.Threads
            });

        foreach (var potentialScene in potentialScenes) {
            scanSceneBlock.Post(potentialScene);
        }

        scanSceneBlock.Complete();
        await scanSceneBlock.Completion;

        await _uuidReferenceResolver.ResolveDelayedReferences();
    }

    private void PrintWarnings(List<JsonFile> scenes, IList<VarPackage> varPackages)
    {
        _progressTracker.Report("Saving logs", forceShow: true);

        _logger.Log("Unable to parse var:");
        var unableToParseVarNames = scenes
            .SelectMany(t => t.Missing)
            .Where(t => t.EstimatedVarName is null && t.Value.Contains(':') && !t.Value.StartsWith("SELF", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var unableToParseVarName in unableToParseVarNames.OrderBy(t => t.Value)) {
            _logger.Log($"'{unableToParseVarName.Value}' in {unableToParseVarName.ForJsonFile}");
        }

        _logger.Log("Unresolved references");
        foreach (var unableToParseVarName in scenes.SelectMany(t => t.Missing).Except(unableToParseVarNames).OrderBy(t => t.Value)) {
            _logger.Log($"'{unableToParseVarName.Value}' in {unableToParseVarName.ForJsonFile}");
        }

        _logger.Log("Missing vars");
        var errors = scenes.SelectMany(t => t.File.IsVar ? t.File.Var.UnresolvedDependencies : t.File.Free.UnresolvedDependencies).Distinct().OrderBy(t => t);
        foreach (var error in errors) {
            _logger.Log(error);
        }

        _logger.Log("Extensions");
        foreach (var seenExtensionsKey in JsonScannerHelper.SeenExtensions.Keys) {
            _logger.Log(seenExtensionsKey);
        }
    }

    private async Task ScanJsonAsync(PotentialJsonFile potentialJson)
    {
        try {
            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _total, potentialJson.Name));
            foreach (var openedJson in potentialJson.OpenJsons())
                await ScanJsonAsync(openedJson, potentialJson);

        } catch (Exception ex) {
            _errors.Add($"[UNKNOWN-ERROR] Unable to process {potentialJson.Name} because: {ex}");
        } finally {
            potentialJson.Dispose();
        }
    }

    private async Task ScanJsonAsync(OpenedPotentialJson openedJson, PotentialJsonFile potentialJson)
    {
        using var streamReader = openedJson.Stream == null ? null : new StreamReader(openedJson.Stream);
        var localJsonPath = _fs.Path.GetDirectoryName(openedJson.File.LocalPath).NormalizePathSeparators();

        Reference? nextScanForUuidOrMorphName = null;
        JsonReference? resolvedReferenceWhenUuidMatchingFails = null;

        var jsonFile = new JsonFile(openedJson);
        var offset = 0;
        var hasDelayedReferences = false;

        if (openedJson.CachedReferences != null) {
            foreach (var reference in openedJson.CachedReferences) {
                (nextScanForUuidOrMorphName, resolvedReferenceWhenUuidMatchingFails) = ProcessJsonReference(reference);

                if (reference.InternalId != null) {
                    if (nextScanForUuidOrMorphName is null) throw new ArgumentException("Uuid reference is null but got internal id");
                    ProcessVamReference(reference);
                } else if (reference.MorphName != null) {
                    if (nextScanForUuidOrMorphName is null) throw new ArgumentException("morph reference is null but got morph name");
                    ProcessMorphReference(reference);
                }
            }
        }

        while (streamReader is { EndOfStream: false }) {
            var line = await streamReader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
                continue;

            if (nextScanForUuidOrMorphName != null) {
                if (line.Contains("\"internalId\"")) {
                    var internalId = line.Replace("\"internalId\"", "");
                    nextScanForUuidOrMorphName.InternalId = internalId[(internalId.IndexOf('\"') + 1)..internalId.LastIndexOf('\"')];
                    ProcessVamReference(nextScanForUuidOrMorphName);

                    nextScanForUuidOrMorphName = null;
                    resolvedReferenceWhenUuidMatchingFails = null;
                    offset += line.Length;
                    continue;
                }

                if (line.Contains("\"name\"")) {
                    var morphName = line.Replace("\"name\"", "");
                    nextScanForUuidOrMorphName.MorphName = morphName[(morphName.IndexOf('\"') + 1)..morphName.LastIndexOf('\"')];
                    ProcessMorphReference(nextScanForUuidOrMorphName);

                    nextScanForUuidOrMorphName = null;
                    resolvedReferenceWhenUuidMatchingFails = null;
                    offset += line.Length;
                    continue;
                }

                if (resolvedReferenceWhenUuidMatchingFails != null) {
                    jsonFile.AddReference(resolvedReferenceWhenUuidMatchingFails);
                    resolvedReferenceWhenUuidMatchingFails = null;
                } else {
                    jsonFile.AddMissingReference(nextScanForUuidOrMorphName);
                }

                nextScanForUuidOrMorphName = null;
            }

            Reference? reference;
            string? referenceParseError;
            try {
                reference = _jsonFileParser.GetAsset(line, offset, openedJson.File, out referenceParseError);
            } catch (Exception e) {
                _logger.Log($"[ERROR] {e.Message} Unable to parse asset '{line}' in {openedJson.File}");
                throw;
            } finally {
                offset += line.Length;
            }

            if (reference is null) {
                if (referenceParseError != null)
                    _errors.Add(referenceParseError);
                continue;
            }

            (nextScanForUuidOrMorphName, resolvedReferenceWhenUuidMatchingFails) = ProcessJsonReference(reference);
        }

        if (jsonFile.References.Count > 0 || jsonFile.Missing.Count > 0 || hasDelayedReferences) {
            _jsonFiles.Add(jsonFile);
            openedJson.File.JsonFile = jsonFile;
        }

        (Reference? nextScanForUuidOrMorphName, JsonReference? jsonReference) ProcessJsonReference(Reference reference)
        {
            JsonReference? jsonReference = null;
            if (reference.Value.Contains(':')) {
                jsonReference = _referencesResolver.ScanPackageSceneReference(potentialJson, reference, reference.Value, localJsonPath);
            }

            if (jsonReference == null && (!reference.Value.Contains(':') ||
                                          (reference.Value.Contains(':') && reference.Value.StartsWith("SELF:", StringComparison.Ordinal)))) {
                jsonReference = _referencesResolver.ScanFreeFileSceneReference(localJsonPath, reference);
                // it can be inside scene in var
                if (jsonReference == default && potentialJson.IsVar) {
                    jsonReference = _referencesResolver.ScanPackageSceneReference(potentialJson, reference, "SELF:" + reference.Value, localJsonPath);
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
            var (jsonReferenceByMorphName, delayedReference) = _uuidReferenceResolver.MatchMorphJsonReferenceByName(jsonFile, morphReference, resolvedReferenceWhenUuidMatchingFails?.ToFile);
            if (jsonReferenceByMorphName != null)
                jsonFile.AddReference(jsonReferenceByMorphName);
            else if (!delayedReference)
                jsonFile.AddMissingReference(morphReference);
            else
                hasDelayedReferences = true;
        }

        void ProcessVamReference(Reference vamReference)
        {
            var (jsonReferenceById, delayedReference) = _uuidReferenceResolver.MatchVamJsonReferenceById(jsonFile, vamReference, resolvedReferenceWhenUuidMatchingFails?.ToFile);
            if (jsonReferenceById != null)
                jsonFile.AddReference(jsonReferenceById);
            else if (!delayedReference)
                jsonFile.AddMissingReference(vamReference);
            else
                hasDelayedReferences = true;
        }
    }
}

public interface IScanJsonFilesOperation : IOperation
{
    Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles, IList<VarPackage> varFiles);
}