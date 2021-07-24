using System;
using System.IO;
using System.Text.RegularExpressions;

namespace VamRepacker.Models
{
    public class VarPackageName //: IEquatable<VarPackageName>
    {
        public static readonly Regex ExtractRegex = new(@"^(?<Author>([^\.]+)|(\*))\.(?<Name>([^\.]+|\*))\.(?<Min>min)?(?<Version>([0-9]+|\*|latest))\.var$", RegexOptions.Compiled, TimeSpan.FromSeconds(0.5));

        public string Filename { get; }
        public string PackageNameWithoutVersion { get; }
        public string Author { get; }
        public string Name { get; }
        public int Version { get; }
        public bool MinVersion { get; }

        public static bool TryGet(string filename, out VarPackageName name)
        {
            var match = ExtractRegex.Match(filename);
            if (!match.Success)
            {
                name = null;
                return false;
            }

            name = new VarPackageName(filename, 
                match.Groups["Author"].Value, 
                match.Groups["Name"].Value, match.Groups["Version"].Value is "*" or "latest" ? -1 : int.Parse(match.Groups["Version"].Value),
                match.Groups["Min"].Success);
            return true;
        }

        private VarPackageName(string filename, string author, string name, int version, bool minVersion)
        {
            Filename = filename;
            Author = author;
            Name = name;
            Version = version;
            MinVersion = minVersion;
            PackageNameWithoutVersion = $"{Author}.{Name}";
        }

        public override string ToString()
        {
            return Name != null ? $"{Name} v{Version} by {Author} ({Filename})" : Filename;
        }

        //public bool Equals(VarPackageName other)
        //{
        //    if (ReferenceEquals(null, other)) return false;
        //    if (ReferenceEquals(this, other)) return true;
        //    return string.Equals(Filename, other.Filename, StringComparison.OrdinalIgnoreCase);
        //}

        //public override bool Equals(object obj)
        //{
        //    if (ReferenceEquals(null, obj)) return false;
        //    if (ReferenceEquals(this, obj)) return true;
        //    if (obj.GetType() != this.GetType()) return false;
        //    return Equals((VarPackageName) obj);
        //}

        //public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Filename);
    }
}
