using System.Collections.Generic;

namespace VamRepacker.Models
{
    public interface IVamObjectWithDependencies
    {
        List<VarPackage> TrimmedResolvedVarDependencies { get; }
        List<VarPackage> AllResolvedVarDependencies { get; }
        List<FreeFile> TrimmedResolvedFreeDependencies { get; }
        List<FreeFile> AllResolvedFreeDependencies { get; }
        IEnumerable<string> UnresolvedDependencies { get; }
        void CalculateDeps();
        void CalculateShallowDeps();
        void ClearDependencies();
    }
}