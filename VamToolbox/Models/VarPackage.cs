using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class VarPackage : IVamObjectWithDependencies
{
    public VarPackageName Name { get; }
    public long Size { get; }
    public string FullPath { get; }
    public bool IsInVaMDir { get; }

    private readonly List<VarPackageFile> _files = new();
    public IReadOnlyList<VarPackageFile> Files => _files;

    private AssetType? _assetType;
    private AssetType? Type => _assetType ??= Files
        .SelectMany(t => t.SelfAndChildren())
        .Aggregate(AssetType.Unknown, (a, b) => a | b.Type);

    private List<JsonFile>? _jsonFiles;
    public IReadOnlyList<JsonFile> JsonFiles => _jsonFiles ??= Files
        .SelectMany(t => t.SelfAndChildren())
        .Where(t => t.JsonFile != null)
        .Select(t => t.JsonFile!)
        .ToList();

    private List<VarPackage>? _trimmedResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies;
    public IReadOnlyList<VarPackage> ResolvedVarDependencies => CalculateDeps().Var;
    public IReadOnlyList<FreeFile> ResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _trimmedResolvedVarDependencies is not null;

    public IEnumerable<string> UnresolvedDependencies => JsonFiles
        .SelectMany(t => t.Missing.Select(x => x.EstimatedReferenceLocation + " from " + t))
        .Distinct();

    private Dictionary<string, VarPackageFile>? _filesDict;

    public Dictionary<string, VarPackageFile> FilesDict => _filesDict ??= Files
        .SelectMany(t => t.SelfAndChildren())
        .GroupBy(t => t.LocalPath, StringComparer.InvariantCultureIgnoreCase)
        .ToDictionary(t => t.Key, t => t.First(), StringComparer.InvariantCultureIgnoreCase);

    public VarPackage(
        VarPackageName name,
        string fullPath,
        bool isInVamDir,
        long size)
    {
        Name = name;
        FullPath = fullPath.NormalizePathSeparators();
        IsInVaMDir = isInVamDir;
        Size = size;
    }

    internal void AddVarFile(VarPackageFile varFile) => _files.Add(varFile);

    public override string ToString() => FullPath;

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(JsonFiles);
    }

    public void ClearDependencies()
    {
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}