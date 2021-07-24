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
        public VarPackage Var { get; private set; }
        public FreeFile Free { get; private set; }

        public string Name => IsVar ? Var.Name.Filename : Free.LocalPath;

        private FileStream _varFileStream;
        private ZipArchive _varArchive;

        public PotentialJsonFile(VarPackage var) => Var = var;
        public PotentialJsonFile(FreeFile free) => Free = free;

        public IEnumerable<(Stream, string)> OpenJsons()
        {
            var extensions = new[] {".json", ".vap", ".vaj"};

            if (extensions.Contains(Free?.ExtLower))
            {
                yield return (File.OpenRead(Free.FullPath), Free.LocalPath);
            }
            else if (Var != null)
            {
                _varFileStream = File.OpenRead(Var.FullPath);
                _varArchive = new ZipArchive(_varFileStream);

                foreach (var entry in _varArchive.Entries.Where(t =>
                    t.Name != "meta.json" && extensions.Contains(Path.GetExtension(t.Name).ToLower())))
                {
                    yield return (entry.Open(), entry.FullName.NormalizePathSeparators());
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
    }
}