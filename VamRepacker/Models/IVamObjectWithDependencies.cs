using System.Collections.Generic;

namespace VamRepacker.Models
{
    public interface IVamObjectWithDependencies
    {

        public List<VarPackage> TrimmedResolvedVarDependencies { get; }
        public List<VarPackage> AllResolvedVarDependencies { get; }

        public List<FreeFile> TrimmedResolvedFreeDependencies { get;  }
        public List<FreeFile> AllResolvedFreeDependencies { get; }

        public IEnumerable<string> UnresolvedDependencies { get; }
        public void CalculateDeps(bool force = false);
    }
}