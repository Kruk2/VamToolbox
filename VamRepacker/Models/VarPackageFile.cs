using System;
using System.Collections.Generic;
using System.Linq;

namespace VamRepacker.Models
{
    public class VarPackageFile : FileReferenceBase//, IEquatable<VarPackageFile>
    {
        public VarPackage ParentVar { get; internal set; }
        public VarPackageFile ParentFile { get; internal set; }

        private readonly List<VarPackageFile> _children = new();
        public override IReadOnlyCollection<VarPackageFile> Children => _children.AsReadOnly();

        public VarPackageFile(string localPath, long size)
            : base(localPath, size)
        {
        }

        public override void AddChildren(FileReferenceBase children)
        {
            _children.Add((VarPackageFile) children);
            ((VarPackageFile) children).ParentFile = this;
        }

        public IEnumerable<VarPackageFile> SelfAndChildren()
        {
            return Children == null ? new[] { this } : Children.Concat(new[] { this });
        }

        public override string ToString()
        {
            return base.ToString() + $" Var: {ParentVar.Name.Filename}";
        }

        //public bool Equals(VarPackageFile other)
        //{
        //    if (ReferenceEquals(null, other)) return false;
        //    if (ReferenceEquals(this, other)) return true;
        //    return Equals(ParentVar, other.ParentVar) && Equals(LocalPath, other.LocalPath);
        //}

        //public override bool Equals(object obj)
        //{
        //    if (ReferenceEquals(null, obj)) return false;
        //    if (ReferenceEquals(this, obj)) return true;
        //    if (obj.GetType() != GetType()) return false;
        //    return Equals((VarPackageFile) obj);
        //}

        //public override int GetHashCode() => HashCode.Combine(ParentVar, LocalPath);
    }
}
