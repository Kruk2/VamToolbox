using VamRepacker.Helpers;

namespace VamRepacker.Models
{
    public class JsonReference //: IEquatable<JsonReference>
    {
        public bool IsVarReference => VarFile != null;
        public FreeFile File { get; }
        public VarPackageFile VarFile { get; }
        public JsonFile FromJson { get; internal set; }
        public Reference Reference { get; }

        public JsonReference(FileReferenceBase file, Reference reference)
        {
            if (file is FreeFile freeFile)
                File = freeFile;
            if (file is VarPackageFile varFile)
                VarFile = varFile;

            Reference = reference;
            file.JsonReferences.Add(this);
        }

        public override string ToString()
        {
            return $"{(File != null ? "SELF" : VarFile.ParentVar.Name)}: " + (File?.ToString() ?? VarFile?.ToString());
        }

        //public bool Equals(JsonReference other)
        //{
        //    if (ReferenceEquals(null, other)) return false;
        //    if (ReferenceEquals(this, other)) return true;
        //    return IsVarReference ? VarFile.Equals(other.VarFile) : File.Equals(other.File);
        //}

        //public override bool Equals(object obj)
        //{
        //    if (ReferenceEquals(null, obj)) return false;
        //    if (ReferenceEquals(this, obj)) return true;
        //    if (obj.GetType() != this.GetType()) return false;
        //    return Equals((JsonReference) obj);
        //}

        //public override int GetHashCode() => IsVarReference ? VarFile.GetHashCode() : File.GetHashCode();
    }
}
