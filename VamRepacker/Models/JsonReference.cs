using System.Diagnostics.CodeAnalysis;
using System.IO;
using VamRepacker.Helpers;

namespace VamRepacker.Models;

public sealed class JsonReference
{
    [MemberNotNullWhen(true, nameof(ParentVar))]
    [MemberNotNullWhen(true, nameof(VarFile))]
    [MemberNotNullWhen(false, nameof(FreeFile))]
    public bool IsVarReference => File is VarPackageFile;
    public FileReferenceBase File { get; }

    public VarPackage? ParentVar => File is VarPackageFile varFile ? varFile.ParentVar : null;
    public FreeFile? FreeFile => File as FreeFile;
    public VarPackageFile? VarFile => File as VarPackageFile;
    public Reference Reference { get; }

    public JsonReference(FileReferenceBase file, Reference reference)
    {
        File = file;
        Reference = reference;
    }

    public override string ToString()
    {
        return $"{(ParentVar is null ? "SELF" : ParentVar.Name)}: " + File;
    }
}