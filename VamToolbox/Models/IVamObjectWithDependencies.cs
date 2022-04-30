namespace VamToolbox.Models;

public interface IVamObjectWithDependencies
{
    IReadOnlyList<VarPackage> TrimmedResolvedVarDependencies { get; }
    IReadOnlyList<VarPackage> AllResolvedVarDependencies { get; }
    IReadOnlyList<FreeFile> TrimmedResolvedFreeDependencies { get; }
    IReadOnlyList<FreeFile> AllResolvedFreeDependencies { get; }
    IEnumerable<string> UnresolvedDependencies { get; }
    bool AlreadyCalculatedDeps { get; }

    void ClearDependencies();
}