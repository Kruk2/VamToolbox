using System;
using System.Collections.Generic;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models
{
    public class FreeFile : FileReferenceBase, IVamObjectWithDependencies
    {
        public string FullPath { get; }
        public List<JsonFile> JsonFiles { get; } = new();

        private readonly List<FreeFile> _children = new();
        public override IReadOnlyCollection<FreeFile> Children => _children.AsReadOnly();

        public List<VarPackage> TrimmedResolvedVarDependencies { get; private set; }
        public List<VarPackage> AllResolvedVarDependencies { get; private set; }
        public List<FreeFile> TrimmedResolvedFreeDependencies { get; private set; }
        public List<FreeFile> AllResolvedFreeDependencies { get; private set; }

        public IEnumerable<string> UnresolvedDependencies => JsonFiles
            .SelectMany(t => t.Missing.Select(x => x.Value + " from " + t))
            .Distinct();

        public bool IsInVaMDir { get; }
        public bool AlreadyCalculatedDeps => AllResolvedFreeDependencies != null;

        public FreeFile(string path, string localPath, long size, bool isInVamDir)
            : base(localPath, size)
        {
            FullPath = path.NormalizePathSeparators();
            IsInVaMDir = isInVamDir;
        }

        public IEnumerable<FreeFile> SelfAndChildren()
        {
            return Children == null ? new[] { this } : Children.Concat(new[] { this });
        }

        public override string ToString() => LocalPath;

        public override void AddChildren(FileReferenceBase children)
        {
            _children.Add((FreeFile) children);
            children.ParentFile = this;
        }

        public void CalculateDeps()
        {
            if (AlreadyCalculatedDeps) return;
            (AllResolvedVarDependencies, AllResolvedFreeDependencies) = DependencyCalculator.CalculateAllVarRecursiveDeps(JsonFiles);
        }

        public void CalculateShallowDeps()
        {
            if (TrimmedResolvedVarDependencies != null) return;
            (TrimmedResolvedVarDependencies, TrimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(JsonFiles);
        }

        public void ClearDependencies()
        {
            AllResolvedFreeDependencies = null;
            AllResolvedVarDependencies = null;
            TrimmedResolvedFreeDependencies = null;
            TrimmedResolvedVarDependencies = null;
        }
    }
}
