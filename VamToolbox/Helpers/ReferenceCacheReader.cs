using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Helpers;

public interface IReferenceCacheReader
{
    Task SaveCache(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles);
    Task ReadCache(List<PotentialJsonFile> potentialScenes);
}

public class ReferenceCacheReader : IReferenceCacheReader
{
    private readonly IDatabase _database;
    private readonly IProgressTracker _progressTracker;
    private Dictionary<string, ILookup<string, ReferenceEntry>>? _globalReferenceCache;

    public ReferenceCacheReader(IDatabase database, IProgressTracker progressTracker)
    {
        _database = database;
        _progressTracker = progressTracker;
    }

    public Task SaveCache(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles) => Task.Run(() => SaveCacheSync(varFiles, freeFiles));

    private void SaveCacheSync(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles)
    {
        _progressTracker.Report("Generating cache", forceShow: true);

        var progress = 0;
        var jsonFilesFromFreeFiles = freeFiles
            .SelectMany(t => t.SelfAndChildren())
            .Where(t => (t.ExtLower is ".vam" or ".vmi" || KnownNames.IsPotentialJsonFile(t.ExtLower)) && t.Dirty);
        var jsonFilesFromVars = varFiles.SelectMany(t => t.Files)
            .SelectMany(t => t.SelfAndChildren())
            .Where(t => (t.ExtLower is ".vam" or ".vmi" || KnownNames.IsPotentialJsonFile(t.ExtLower)) && t.Dirty); // ugly, forces to save cache for internalId/morphName

        var jsonFiles = jsonFilesFromVars.Cast<FileReferenceBase>().Concat(jsonFilesFromFreeFiles).ToList();
        var total = jsonFiles.Count + jsonFiles.Count;

        var bulkInsertFiles = new Dictionary<FileReferenceBase, long>();
        var bulkInsertReferences = new List<(FileReferenceBase file, IEnumerable<Reference> references)>();

        foreach (var file in jsonFiles) {
            bulkInsertFiles[file] = 0;
            if (file.JsonFile is not null) {
                var references = file.JsonFile.References.Select(t => t.Reference).Concat(file.JsonFile.Missing);
                bulkInsertReferences.Add((file, references));
            }

            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref progress), total, $"Caching {file.LocalPath}"));
        }

        _progressTracker.Report("Saving file cache", forceShow: true);
        _database.SaveFiles(bulkInsertFiles);
        _progressTracker.Report("Saving references cache", forceShow: true);
        _database.UpdateReferences(bulkInsertFiles, bulkInsertReferences);
    }

    public Task ReadCache(List<PotentialJsonFile> potentialScenes) => Task.Run(() => ReadCacheSync(potentialScenes));

    public void ReadCacheSync(List<PotentialJsonFile> potentialScenes)
    {
        var progress = 0;
        HashSet<VarPackage> processedVars = new();
        HashSet<FreeFile> processedFreeFiles = new();

        _progressTracker.Report(new ProgressInfo(0, potentialScenes.Count, "Fetching cache from database", forceShow: true));

        _globalReferenceCache = _database.ReadReferenceCache()
            .GroupBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(t => t.Key, t => t.ToLookup(x => x.LocalPath));

        foreach (var json in potentialScenes) {
            switch (json.IsVar) {
                case true when processedVars.Add(json.Var):
                case false when processedFreeFiles.Add(json.Free):
                    ReadReferenceCache(json);
                    break;
            }

            _progressTracker.Report(new ProgressInfo(progress++, potentialScenes.Count, "Reading cache: " + (json.IsVar ? json.Var.ToString() : json.Free.ToString())));
        }
    }

    private void ReadReferenceCache(PotentialJsonFile potentialJsonFile)
    {
        if (_globalReferenceCache is null) throw new InvalidOperationException("Cache not initialized");

        if (potentialJsonFile.IsVar) {
            foreach (var varFile in potentialJsonFile.Var.Files
                         .SelectMany(t => t.SelfAndChildren())
                         .Where(t => t.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(t.ExtLower))
                         .Where(t => !t.Dirty)) {
                if (_globalReferenceCache.TryGetValue(varFile.ParentVar.FullPath, out var references) && references.Contains(varFile.LocalPath)) {
                    var mappedReferences = references[varFile.LocalPath].Where(x => x.Value is not null).Select(t => new Reference(t, varFile)).ToList();
                    potentialJsonFile.AddCachedReferences(varFile.LocalPath, mappedReferences);
                }
            }
        } else if (!potentialJsonFile.IsVar && !potentialJsonFile.Free.Dirty) {
            var free = potentialJsonFile.Free;
            if (_globalReferenceCache.TryGetValue(free.FullPath, out var references)) {
                var mappedReferences = references[string.Empty].Where(x => x.Value is not null).Select(t => new Reference(t, free)).ToList();
                potentialJsonFile.AddCachedReferences(mappedReferences);
            }
        }
    }
}