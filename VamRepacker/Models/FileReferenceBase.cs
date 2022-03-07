using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreLinq;
using VamRepacker.Hashing;
using VamRepacker.Helpers;

namespace VamRepacker.Models
{
    public abstract class FileReferenceBase
    {
        public string LocalPath { get; }
        public string FilenameLower { get; }
        public string FilenameWithoutExt { get; }
        public string ExtLower { get; }

        public string Hash { get; internal set; }
        public string InternalId { get; internal set; }
        public string MorphName { get; internal set; }
        public string VamAuthor { get; internal set; }

        public List<JsonReference> JsonReferences { get; } = new();

        public long Size { get; }
        public FileReferenceBase ParentFile { get; protected internal set; }
        public abstract IReadOnlyCollection<FileReferenceBase> Children { get; }
        public List<string> MissingChildren { get; } = new();

        private string _hashWithChildren;
        public string HashWithChildren => _hashWithChildren ??= MD5Helper.GetHash(Hash, Children.Select(t => t.Hash));
        public long SizeWithChildren => Size + Children.Sum(t => t.SizeWithChildren);

        protected FileReferenceBase(string localPath, long size)
        {
            LocalPath = localPath.NormalizePathSeparators();
            FilenameLower = Path.GetFileName(localPath).ToLowerInvariant();
            FilenameWithoutExt = Path.GetFileNameWithoutExtension(localPath);
            ExtLower = Path.GetExtension(FilenameLower);
            Size = size;
        }

        public override string ToString() => LocalPath;

        public abstract void AddChildren(FileReferenceBase children);

        public void AddMissingChildren(string localChildrenPath) => MissingChildren.Add(localChildrenPath);
    }
}
