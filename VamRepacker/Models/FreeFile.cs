using System;
using System.Collections.Generic;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models
{
    public class FreeFile : FileReferenceBase, IVamObjectWithDependencies//, IEquatable<FreeFile>
    {
        public string FullPath { get; }
        public FreeFile ParentFile { get; private set; }
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
        
        public FreeFile(string path, string localPath, long size)
            : base(localPath, size)
        {
            FullPath = path.NormalizePathSeparators();
        }

        public IEnumerable<FreeFile> SelfAndChildren()
        {
            return Children == null ? new[] { this } : Children.Concat(new[] { this });
        }

        public override string ToString() => LocalPath;

        public override void AddChildren(FileReferenceBase children)
        {
            _children.Add((FreeFile) children);
            ((FreeFile) children).ParentFile = this;
        }

        public void CalculateDeps(bool force = false)
        {
            if (TrimmedResolvedFreeDependencies != null && !force) return;
            (TrimmedResolvedVarDependencies, TrimmedResolvedFreeDependencies) = DependencyCalculator.CalculateVarRecursiveDeps(JsonFiles);
            (AllResolvedVarDependencies, AllResolvedFreeDependencies) = DependencyCalculator.CalculateAllVarRecursiveDeps(JsonFiles);
        }

        public void ClearDependencies()
        {
            AllResolvedFreeDependencies = null;
            AllResolvedVarDependencies = null;
            TrimmedResolvedFreeDependencies = null;
            TrimmedResolvedVarDependencies = null;
        }

        //public bool Equals(FreeFile other)
        //{
        //    if (ReferenceEquals(null, other)) return false;
        //    if (ReferenceEquals(this, other)) return true;
        //    return FullPath == other.FullPath;
        //}

        //public override bool Equals(object obj)
        //{
        //    if (ReferenceEquals(null, obj)) return false;
        //    if (ReferenceEquals(this, obj)) return true;
        //    if (obj.GetType() != this.GetType()) return false;
        //    return Equals((FreeFile) obj);
        //}

        //public override int GetHashCode() => FullPath.GetHashCode();
    }
}
