namespace VamToolbox.Models;

public interface IVamObjectWithDependencies
{
    IReadOnlyList<VarPackage> ResolvedVarDependencies { get; }
    IReadOnlyList<FreeFile> ResolvedFreeDependencies { get; }
    IEnumerable<string> UnresolvedDependencies { get; }
    bool AlreadyCalculatedDeps { get; }

    void ClearDependencies();
}