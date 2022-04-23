using VamRepacker.Helpers;

namespace VamRepacker.Models;

public sealed class JsonReference
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
}