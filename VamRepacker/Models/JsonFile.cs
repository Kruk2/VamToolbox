using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models;

public sealed class JsonFile
{
    [MemberNotNullWhen(true, nameof(VarFile))]
    [MemberNotNullWhen(true, nameof(Var))]
    [MemberNotNullWhen(true, nameof(JsonPathInVar))]
    [MemberNotNullWhen(false, nameof(Free))]
    public bool IsVar => File is VarPackageFile;
    public FileReferenceBase File { get; }

    public VarPackage? Var => File is VarPackageFile varFile ? varFile.ParentVar : null;
    public VarPackageFile? VarFile => File as VarPackageFile;
    public FreeFile? Free => File as FreeFile;

    public string Name => IsVar ? (JsonPathInVar + " in " + Var.Name.Filename) : Free.LocalPath;

    public List<JsonReference> References { get; }
    public HashSet<VarPackage> VarReferences { get; }
    public HashSet<FreeFile> FreeReferences { get; }
    public List<Reference> Missing { get; }
    public string? JsonPathInVar { get; }

    public JsonFile(PotentialJsonFile file, string? jsonPathInVar, List<JsonReference> references, List<Reference> missing)
    {
        if (file.IsVar)
        {
            if (jsonPathInVar is null) throw new ArgumentNullException(nameof(jsonPathInVar),  $"Var: {file}");
            var varFile = file.Var.FilesDict[jsonPathInVar];
            File = varFile;
            varFile.ParentVar.JsonFiles.Add(this);
            varFile.JsonFiles.Add(this);
        }
        else
        {
            File = file.Free;
            file.Free.JsonFiles.Add(this);
        }

        References = references;
        Missing = missing;
        JsonPathInVar = jsonPathInVar;

        References.ForEach(t => t.FromJson = this);
        References.Select(t => t.Reference).Concat(missing).ToList().ForEach(t => t.FromJson = this);
        VarReferences = new HashSet<VarPackage>(References.Where(t => t.IsVarReference).Select(t => t.ParentVar!));
        FreeReferences = new HashSet<FreeFile>(References.Where(t => !t.IsVarReference).Select(t => t.FreeFile!));
    }

    public override string ToString()
    {
        return IsVar ? (JsonPathInVar + " var: " + Var.Name.Filename) : Free.ToString();
    }
}