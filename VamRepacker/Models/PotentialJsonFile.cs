using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models
{
    public class PotentialJsonFile : IDisposable
    {
        public bool IsVar => Var != null;
        public VarPackage Var { get; }
        public FreeFile Free { get; }

        public string Name => IsVar ? Var.Name.Filename : Free.LocalPath;

        private FileStream _varFileStream;
        private ZipArchive _varArchive;

        private readonly Dictionary<string, List<Reference>> _referenceCache = new(StringComparer.OrdinalIgnoreCase);
        public PotentialJsonFile(VarPackage var) => Var = var;
        public PotentialJsonFile(FreeFile free) => Free = free;

        public IEnumerable<OpenedPotentialJson> OpenJsons()
        {
            if (_referenceCache.Any())
            {
                foreach (var (localJsonPath, value) in _referenceCache)
                {
                    yield return new OpenedPotentialJson { LocalJsonPath = localJsonPath, CachedReferences = value };
                }

                yield break;
            }

            if (Free != null && KnownNames.IsPotentialJsonFile(Free.ExtLower))
            {
                yield return new OpenedPotentialJson { Stream = File.OpenRead(Free.FullPath), LocalJsonPath = Free.LocalPath };
            }
            else if (Var != null)
            {
                _varFileStream = File.OpenRead(Var.FullPath);
                _varArchive = new ZipArchive(_varFileStream);

                foreach (var entry in _varArchive.Entries.Where(t =>
                             t.Name != "meta.json" && KnownNames.IsPotentialJsonFile(Path.GetExtension(t.Name).ToLower())))
                {
                    var localJsonPath = entry.FullName.NormalizePathSeparators();
                    yield return new OpenedPotentialJson { LocalJsonPath = localJsonPath, Stream = entry.Open() };
                }
            }
        }

        public void Dispose()
        {
            _varFileStream?.Dispose();
            _varArchive?.Dispose();
        }

        public override string ToString()
        {
            return IsVar ? Var.ToString() : Free.ToString();
        }

        public void AddCachedReferences(string fileLocalPath, List<Reference> references)
        {
            _referenceCache[fileLocalPath] = references;
        }

        public void AddCachedReferences(List<Reference> references)
        {
            if (Free == null) throw new ArgumentException("Unable to add cached references for non-free file");
            _referenceCache[Free.LocalPath] = references;
        }
    }

    public class OpenedPotentialJson
    {
        public Stream Stream { get; init; }
        public string LocalJsonPath { get; init; }
        public List<Reference> CachedReferences { get; init; }
    }
}