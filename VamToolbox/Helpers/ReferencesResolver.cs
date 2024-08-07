﻿using System.Collections.Concurrent;
using System.IO.Abstractions;
using MoreLinq;
using VamToolbox.Models;

namespace VamToolbox.Helpers;

public interface IReferencesResolver
{
    JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, VarPackage? varToSearch, string localSceneFolder);
    JsonReference? ScanFreeFileSceneReference(string localSceneFolder, Reference reference);
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

    public JsonReference? ScanFreeFileSceneReference(string localSceneFolder, Reference reference)
    {
        var refPath = reference.EstimatedReferenceLocation;
        // searching in localSceneFolder for var json files is handled in ScanPackageSceneReference
        if (!reference.ForJsonFile.IsVar && _freeFilesIndex[_fs.SimplifyRelativePath(localSceneFolder, refPath)] is var f1 && f1.Any()) {
            f1 = f1.OrderByDescending(t => t.UsedByVarPackagesOrFreeFilesCount).ThenBy(t => t.FullPath);
            var x = f1.FirstOrDefault(t => t.IsInVaMDir) ?? f1.First();
            return new JsonReference(x, reference);
        }
        if (_freeFilesIndex[refPath] is var f2 && f2.Any()) {
            f2 = f2.OrderByDescending(t => t.UsedByVarPackagesOrFreeFilesCount).ThenBy(t => t.FullPath);
            var x = f2.FirstOrDefault(t => t.IsInVaMDir) ?? f2.First();
            return new JsonReference(x, reference);
        }

        return default;
    }

    public JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, VarPackage? varToSearch, string localSceneFolder)
    {
        if (varToSearch is null) {
            var varFile = reference.EstimatedVarName;
            if (varFile is null) {
                _errors.Add($"[ASSET-PARSE-ERROR] {reference.Value} was neither a SELF reference or VAR in {potentialJson}");
                return default;
            }

            varToSearch = FindVar(varFile);
        }

        if (varToSearch != null) {
            var varAssets = varToSearch.FilesDict;
            var assetName = reference.EstimatedReferenceLocation;

            if (potentialJson.Var == varToSearch) {
                var refInScene = _fs.SimplifyRelativePath(localSceneFolder, assetName);
                if (varAssets.TryGetValue(refInScene, out var f1)) {
                    //_logger.Log($"[RESOLVER] Found f1 {f1.ToParentVar.Name.Filename} for reference {refer}")}");ence.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath
                    return new JsonReference(f1, reference);
                }
            }

            if (varAssets.TryGetValue(assetName, out var f2)) {
                //_logger.Log($"[RESOLVER] Found f2 {f2.ToParentVar.Name.Filename} for reference {reference.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath}")}");
                return new JsonReference(f2, reference);
            }
        }

        return null;
    }

    private VarPackage? FindVar(VarPackageName varFile)
    {
        if (!_varFilesIndex.Contains(varFile.PackageNameWithoutVersion)) {
            return null;
        }

        var possibleVarsToSearch = _varFilesIndex[varFile.PackageNameWithoutVersion];
        if (varFile.MinVersion)
        {
            possibleVarsToSearch = _varFilesIndex[varFile.PackageNameWithoutVersion].Where(t => t.Name.Version >= varFile.Version);
        }
        else if (varFile.Version != -1)
        {
            possibleVarsToSearch = _varFilesIndex[varFile.PackageNameWithoutVersion].Where(t => t.Name.Version == varFile.Version);
        }

        // VAM will use latest available version when exact match was not found
        return possibleVarsToSearch.Maxima(t => t.Name.Version).MinBy(t => t.FullPath.Length) ??
                      _varFilesIndex[varFile.PackageNameWithoutVersion].Maxima(t => t.Name.Version).MinBy(t => t.FullPath.Length);
    }
}