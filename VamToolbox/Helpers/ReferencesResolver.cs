using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using MoreLinq;
using VamToolbox.Models;

namespace VamToolbox.Helpers;

public interface IReferencesResolver
{
    JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, string refPath, string? localSceneFolder);
    JsonReference? ScanFreeFileSceneReference(string? localSceneFolder, Reference reference);
    Task InitLookups(IList<FreeFile> freeFiles, IList<VarPackage> varFiles, ConcurrentBag<string> errors);
}

public class ReferencesResolver : IReferencesResolver
{
    private readonly IFileSystem _fs;
    private ILookup<string, FreeFile> _freeFilesIndex = null!;
    private ILookup<string, VarPackage> _varFilesIndex = null!;
    private ConcurrentBag<string> _errors = null!;

    public ReferencesResolver(IFileSystem fs) => _fs = fs;

    public Task InitLookups(IList<FreeFile> freeFiles, IList<VarPackage> varFiles, ConcurrentBag<string> errors) => Task.Run(() => InitLookupsSync(freeFiles, varFiles, errors));

    private void InitLookupsSync(IList<FreeFile> freeFiles, IList<VarPackage> varFiles, ConcurrentBag<string> errors)
    {
        _freeFilesIndex = freeFiles.ToLookup(f => f.LocalPath, f => f, StringComparer.InvariantCultureIgnoreCase);
        _varFilesIndex = varFiles.ToLookup(t => t.Name.PackageNameWithoutVersion, StringComparer.InvariantCultureIgnoreCase);
        _errors = errors;
    }

    public JsonReference? ScanFreeFileSceneReference(string? localSceneFolder, Reference reference)
    {
        if (reference.Value.Contains(':') && !reference.Value.StartsWith("SELF:", StringComparison.Ordinal))
            throw new VamToolboxException($"{reference.ForJsonFile} {reference.Value} refers to var but processing free file reference");

        var refPath = reference.Value.Split(':').Last();
        refPath = refPath.NormalizeAssetPath();
        // searching in localSceneFolder for var json files is handled in ScanPackageSceneReference
        if (!reference.ForJsonFile.IsVar && localSceneFolder is not null && _freeFilesIndex[_fs.Path.Combine(localSceneFolder, refPath).NormalizePathSeparators()] is var f1 && f1.Any())
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

    public JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, string refPath, string? localSceneFolder)
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

            if (potentialJson.Var == varToSearch && localSceneFolder is not null)
            {
                var refInScene = _fs.Path.Combine(localSceneFolder, assetName).NormalizePathSeparators();
                if (varAssets.TryGetValue(refInScene, out var f1))
                {
                    //_logger.Log($"[RESOLVER] Found f1 {f1.ToParentVar.Name.Filename} for reference {refer}")}");ence.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath
                    return new JsonReference(f1, reference);
                }
            }

            if (varAssets.TryGetValue(assetName, out var f2))
            {
                //_logger.Log($"[RESOLVER] Found f2 {f2.ToParentVar.Name.Filename} for reference {reference.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath}")}");
                return new JsonReference(f2, reference);
            }
        }

        return null;
    }
}