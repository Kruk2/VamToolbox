using System.Collections.Generic;

namespace VamRepacker.Models
{
    public interface IVamObjectWithDependencies
    {

        public List<VarPackage> AllResolvedVarDependencies { get; }
        public List<FreeFile> AllResolvedFreeDependencies { get; }

        public IEnumerable<string> UnresolvedDependencies { get; }
        public void CalculateDeps();
        public void CalculateShallowDeps();
        void ClearDependencies();
    }
}