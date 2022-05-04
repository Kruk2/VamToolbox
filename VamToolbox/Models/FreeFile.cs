using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class FreeFile : FileReferenceBase, IVamObjectWithDependencies
{
    public string FullPath { get; }
    private readonly List<FreeFile> _children = new();
    public override IReadOnlyCollection<FreeFile> Children => _children.AsReadOnly();

    private List<VarPackage>? _trimmedResolvedVarDependencies, _allResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies, _allResolvedFreeDependencies;
    public IReadOnlyList<VarPackage> TrimmedResolvedVarDependencies => CalculateShallowDeps().Var;
    public IReadOnlyList<VarPackage> AllResolvedVarDependencies => CalculateDeps().Var;
    public IReadOnlyList<FreeFile> TrimmedResolvedFreeDependencies => CalculateShallowDeps().Free;
    public IReadOnlyList<FreeFile> AllResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _allResolvedVarDependencies is not null || _trimmedResolvedVarDependencies is not null;
    public IEnumerable<string> UnresolvedDependencies => JsonFile?.Missing.Select(x => x.Value + " from " + this) ?? Enumerable.Empty<string>();

    public FreeFile(string path, string localPath, long size, bool isInVamDir, DateTime modifiedTimestamp)
        : base(localPath, size, isInVamDir, modifiedTimestamp)
    {
        FullPath = path.NormalizePathSeparators();
    }

    public IEnumerable<FreeFile> SelfAndChildren() => Children.Append(this);

    public override string ToString() => FullPath;

    public override void AddChildren(FileReferenceBase children)
    {
        _children.Add((FreeFile) children);
        children.ParentFile = this;
    }

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_allResolvedFreeDependencies is not null && _allResolvedVarDependencies is not null)
            return (_allResolvedVarDependencies, _allResolvedFreeDependencies);
        if(JsonFile is null)
            return (_allResolvedVarDependencies, _allResolvedFreeDependencies) = (Enumerable.Empty<VarPackage>().ToList(), Enumerable.Empty<FreeFile>().ToList());

        return (_allResolvedVarDependencies, _allResolvedFreeDependencies) = DependencyCalculator.CalculateAllVarRecursiveDeps(new List<JsonFile> { JsonFile });
    }

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateShallowDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        if (JsonFile is null)
            return (_allResolvedVarDependencies, _allResolvedFreeDependencies) = (Enumerable.Empty<VarPackage>().ToList(), Enumerable.Empty<FreeFile>().ToList());

        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(new List<JsonFile>{JsonFile});
    }

    public void ClearDependencies()
    {
        _allResolvedFreeDependencies = null;
        _allResolvedVarDependencies = null;
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}