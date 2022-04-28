using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class VarPackage : IVamObjectWithDependencies
{
    public VarPackageName Name { get; }
    public long Size { get; set; }
    public string FullPath { get; }
    public bool IsInVaMDir { get; }
    public List<VarPackageFile> Files { get; }

    private List<JsonFile>? _jsonFiles;
    public List<JsonFile> JsonFiles => _jsonFiles ??= Files.SelectMany(t => t.SelfAndChildren()).Where(t => t.JsonFile != null).Select(t => t.JsonFile!).ToList();

    private List<VarPackage>? _trimmedResolvedVarDependencies, _allResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies, _allResolvedFreeDependencies;
    public List<VarPackage> TrimmedResolvedVarDependencies => CalculateShallowDeps().Var;
    public List<VarPackage> AllResolvedVarDependencies => CalculateDeps().Var;
    public List<FreeFile> TrimmedResolvedFreeDependencies => CalculateShallowDeps().Free;
    public List<FreeFile> AllResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _allResolvedVarDependencies is not null || _trimmedResolvedVarDependencies is not null;

    public IEnumerable<string> UnresolvedDependencies => JsonFiles
        .SelectMany(t => t.Missing.Select(x => x.Value + " from " + t))
        .Distinct();

    private Dictionary<string, VarPackageFile>? _filesDict;

    public Dictionary<string, VarPackageFile> FilesDict => _filesDict ??= Files
        .SelectMany(t => t.SelfAndChildren())
        .GroupBy(t => t.LocalPath, StringComparer.InvariantCultureIgnoreCase)
        .ToDictionary(t => t.Key, t => t.First(), StringComparer.InvariantCultureIgnoreCase);

    public VarPackage(
        VarPackageName name, 
        string fullPath, 
        List<VarPackageFile> files,
        bool isInVamDir,
        long size)
    {
        Name = name;
        FullPath = fullPath.NormalizePathSeparators();
        Files = files;
        IsInVaMDir = isInVamDir;
        Size = size;
    }

    public override string ToString() => Name.ToString();

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_allResolvedFreeDependencies is not null && _allResolvedVarDependencies is not null) 
            return (_allResolvedVarDependencies, _allResolvedFreeDependencies);
        return (_allResolvedVarDependencies, _allResolvedFreeDependencies) = DependencyCalculator.CalculateAllVarRecursiveDeps(JsonFiles);
    }

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateShallowDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null) 
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(JsonFiles);
    }

    public void ClearDependencies()
    {
        _allResolvedFreeDependencies = null;
        _allResolvedVarDependencies = null;
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}