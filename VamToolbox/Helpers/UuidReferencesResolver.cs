using System.Collections.Concurrent;
using System.Diagnostics;
using VamToolbox.Models;

namespace VamToolbox.Helpers;
public interface IUuidReferenceResolver
{
    (JsonReference? jsonReference, bool isDelayed) MatchVamJsonReferenceById(JsonFile jsonFile, Reference reference, VarPackage? sourceVar, FileReferenceBase? fallBackResolvedAsset);
    (JsonReference? jsonReference, bool isDelayed) MatchMorphJsonReferenceByName(JsonFile jsonFile, Reference reference, VarPackage? sourceVar, FileReferenceBase? fallBackResolvedAsset);
    Task<List<JsonReference>> ResolveDelayedReferences();
    Task InitLookups(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles);
}

public class UuidReferencesResolver : IUuidReferenceResolver
{
    private ILookup<string, FileReferenceBase> _vamFilesById = null!;
    private ILookup<string, FileReferenceBase> _morphFilesByName = null!;
    private readonly ConcurrentBag<(JsonFile jsonFile, Reference reference, IEnumerable<FileReferenceBase> matchedFiles, string uuidOrName)> _delayedReferencesToResolve = new();
    private readonly Dictionary<(string uuidOrName, byte femaleOrMale), FileReferenceBase> _cachedDeleyedVam = new(new CustomUuidComparer());
    private readonly Dictionary<(string uuidOrName, byte femaleOrMale), FileReferenceBase> _cachedDeleyedMorphs = new(new CustomUuidComparer());

    private class CustomUuidComparer : IEqualityComparer<(string uuidOrName, byte femaleOrMale)>
    {
        public bool Equals((string uuidOrName, byte femaleOrMale) x, (string uuidOrName, byte femaleOrMale) y)
        {
            return string.Equals(x.uuidOrName, y.uuidOrName, StringComparison.OrdinalIgnoreCase) && x.femaleOrMale == y.femaleOrMale;
        }

        public int GetHashCode((string uuidOrName, byte femaleOrMale) obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.uuidOrName, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(obj.femaleOrMale);
            return hashCode.ToHashCode();
        }
    }


    public async Task InitLookups(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles)
    {
        await Task.Run(() => InitVamFilesById(freeFiles, varFiles));
        await Task.Run(() => InitMorphNames(freeFiles, varFiles));
    }

    private void InitVamFilesById(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles)
    {
        var vamFilesFromVars = varFiles
            .SelectMany(t => t.Files.Where(x => x.InternalId != null));

        _vamFilesById = vamFilesFromVars.Cast<FileReferenceBase>()
            .Concat(freeFiles.Where(t => t.InternalId != null && (t.Type & AssetType.ClothOrHair) != 0))
            .Where(t => (t.Type & AssetType.ClothOrHair) != 0)
            .ToLookup(t => t.InternalId!);
    }

    private void InitMorphNames(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles)
    {
        var morphFilesFromVars = varFiles
            .SelectMany(t => t.Files.Where(x => x.MorphName != null && (x.Type & AssetType.Morph) != 0));

        _morphFilesByName = freeFiles
            .Where(t => t.MorphName != null && (t.Type & AssetType.Morph) != 0)
            .Cast<FileReferenceBase>()
            .Concat(morphFilesFromVars)
            .ToLookup(t => t.MorphName!);
    }

    public Task<List<JsonReference>> ResolveDelayedReferences() => Task.Run(ResolveDelayedReferencesSync);

    private List<JsonReference> ResolveDelayedReferencesSync()
    {
        if (_delayedReferencesToResolve.IsEmpty)
            return new List<JsonReference>();

        var createdReferences = new List<JsonReference>(_delayedReferencesToResolve.Count);
        foreach (var (jsonFile, reference, matchedAssets, uuidOrName) in _delayedReferencesToResolve)
        {
            var isVam = reference.Value.EndsWith(".vam", StringComparison.OrdinalIgnoreCase);

            void AddJsonReference(JsonReference referenceToAdd)
            {
                jsonFile.AddReference(referenceToAdd);
            }

            void AddReference(FileReferenceBase fileToAdd)
            {
                var referenceToAdd = new JsonReference(fileToAdd, reference);
                AddJsonReference(referenceToAdd);
            }

            var isFemaleReference = reference.EstimatedAssetType.IsFemale();
            var isMaleReference = reference.EstimatedAssetType.IsMale();
            var femaleOrMaleReference = (byte)(isFemaleReference ? 2 : isMaleReference ? 1 : 0);

            if (isVam && _cachedDeleyedVam.TryGetValue((uuidOrName, femaleOrMaleReference), out var cachedReference))
            {
                AddReference(cachedReference);
                continue;
            }

            if (!isVam && _cachedDeleyedMorphs.TryGetValue((uuidOrName, femaleOrMaleReference), out var cachedMorphReference))
            {
                AddReference(cachedMorphReference);
                continue;
            }

            // prefer vars/json with least dependencies outside vam folder
            var objectsWithDependencies = matchedAssets
                .Select(t => t is VarPackageFile varFile ? varFile.ParentVar : (IVamObjectWithDependencies)t)
                .ToList();

            objectsWithDependencies.ForEach(t => _ = t.TrimmedResolvedVarDependencies);

            var dependenciesCount = objectsWithDependencies.Select(t =>
                t.TrimmedResolvedVarDependencies.Count(t => !t.IsInVaMDir) + t.TrimmedResolvedFreeDependencies.Count(t => !t.IsInVaMDir));
            var minCount = dependenciesCount.Min();
            if (dependenciesCount.All(t => t == 0))
            {
                // if they are all 0 then prefer min dependencies overall
                dependenciesCount = objectsWithDependencies.Select(t =>
                    t.TrimmedResolvedVarDependencies.Count + t.TrimmedResolvedFreeDependencies.Count);
                minCount = dependenciesCount.Min();
            }

            var zipped = matchedAssets.Zip(dependenciesCount);

            var bestMatches = zipped.Where(t => t.Second == minCount).Select(t => t.First);
            FileReferenceBase bestMatch;

            if (bestMatches.Take(2).Count() == 1)
            {
                bestMatch = bestMatches.First();
            }
            else
            {
                var byMostUsedFile = MoreLinq.MoreEnumerable.MaxBy(bestMatches, t => t.UsedByVarPackagesOrFreeFilesCount);
                var bySmallestSize = MoreLinq.MoreEnumerable.MinBy(byMostUsedFile,
                    t => t is VarPackageFile varFile ? varFile.ParentVar.Size : ((FreeFile)t).SizeWithChildren);
                var byNewerVar = MoreLinq.MoreEnumerable.MaxBy(bySmallestSize, t => t.IsVar ? t.Var.Name.Version : int.MaxValue);
                bestMatch = byNewerVar.OrderBy(t => t.ToString()).First();
            }

            AddReference(bestMatch);

            var isFemaleAsset = bestMatch.Type.IsFemale();
            var isMaleAsset = bestMatch.Type.IsMale();
            var femaleOrMale = (byte)(isFemaleAsset ? 2 : isMaleAsset ? 1 : 0);
            if (isVam)
                _cachedDeleyedVam[(uuidOrName, femaleOrMale)] = bestMatch;
            else
                _cachedDeleyedMorphs[(uuidOrName, femaleOrMale)] = bestMatch;
        }

        _delayedReferencesToResolve.Clear();
        return createdReferences;
    }

    public (JsonReference? jsonReference, bool isDelayed) MatchVamJsonReferenceById(JsonFile jsonFile, Reference reference, VarPackage? sourceVar, FileReferenceBase? fallBackResolvedAsset)
    {
        return MatchAssetByUuidOrName(jsonFile, reference.InternalId, reference, _vamFilesById, sourceVar, fallBackResolvedAsset);
    }

    public (JsonReference? jsonReference, bool isDelayed) MatchMorphJsonReferenceByName(JsonFile jsonFile, Reference reference, VarPackage? sourceVar, FileReferenceBase? fallBackResolvedAsset)
    {
        return MatchAssetByUuidOrName(jsonFile, reference.MorphName, reference, _morphFilesByName, sourceVar, fallBackResolvedAsset);
    }

    private (JsonReference? jsonReference, bool isDelayed) MatchAssetByUuidOrName(JsonFile jsonFile, string? uuidOrName,
        Reference reference, ILookup<string, FileReferenceBase> lookup, VarPackage? sourceVar, FileReferenceBase? fallBackResolvedAsset)
    {
        if (string.IsNullOrWhiteSpace(uuidOrName))
            throw new VamToolboxException("Invalid displayNameOrUuid");

        if (fallBackResolvedAsset is not null)
        {
            if (fallBackResolvedAsset.IsInVaMDir) 
                return (new JsonReference(fallBackResolvedAsset, reference), false);
            if (sourceVar is not null && fallBackResolvedAsset.Var == sourceVar)
                return (new JsonReference(fallBackResolvedAsset, reference), false);
            if (reference.EstimatedVarName is not null && (reference.EstimatedVarName.Version != -1 || reference.EstimatedVarName.MinVersion))
                return (new JsonReference(fallBackResolvedAsset, reference), false);
        }

        var matchedAssets = lookup[uuidOrName];
        if (fallBackResolvedAsset is not null && !matchedAssets.Contains(fallBackResolvedAsset)) matchedAssets = matchedAssets.Append(fallBackResolvedAsset);

        var isSupportedType = (reference.EstimatedAssetType & AssetType.ClothOrHairOrMorph) != 0;
        if (isSupportedType)
        {
            var isFemaleAsset = reference.EstimatedAssetType.IsFemale();
            var isMaleAsset = reference.EstimatedAssetType.IsMale();

            if (matchedAssets.Any(x => (x.Type & AssetType.Female) != 0 && isFemaleAsset || (x.Type & AssetType.Male) != 0 && isMaleAsset))
                matchedAssets = matchedAssets.Where(x => (x.Type & AssetType.Female) != 0 && isFemaleAsset || (x.Type & AssetType.Male) != 0 && isMaleAsset);
            else if (fallBackResolvedAsset != null)
                return (new JsonReference(fallBackResolvedAsset, reference), false);
            else
                return (null, false);
        }

        var matchedAsset = matchedAssets.Take(2).ToArray();
        switch (matchedAsset.Length)
        {
            case 0:
                return (null, false);
            case 1:
                return (new JsonReference(matchedAsset[0], reference), false);
        }

        // prefer files inside VAM dir
        if (matchedAssets.Any(t => t.IsInVaMDir))
            matchedAssets = matchedAssets.Where(t => t.IsInVaMDir);

        matchedAsset = matchedAssets.Take(2).ToArray();
        if (matchedAsset.Length == 1)
            return (new JsonReference(matchedAsset[0], reference), false);

        // prefer files that are in var we're scanning
        var anythingFromSourceVar = matchedAssets.Where(t => t.Var == sourceVar).MinBy(t => t.ToString());
        if (sourceVar is not null && anythingFromSourceVar is not null)
        {
            return (new JsonReference(anythingFromSourceVar, reference), false);
        }

        _delayedReferencesToResolve.Add((jsonFile, reference, matchedAssets, uuidOrName));

        return (null, true);
    }
}