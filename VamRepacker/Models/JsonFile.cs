using System.Collections.Generic;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models;

public sealed class JsonFile
{
    public bool IsVar => Var != null;
    public VarPackage Var { get; }
    public VarPackageFile VarFile { get; }
    public FreeFile Free { get; }

    public string Name => IsVar ? (JsonPathInVar + " in " + Var.Name.Filename) : Free.LocalPath;

    public List<JsonReference> References { get; }
    public HashSet<VarPackage> VarReferences { get; }
    public HashSet<FreeFile> FreeReferences { get; }
    public List<Reference> Missing { get; }
    public string JsonPathInVar { get; }

    public JsonFile(PotentialJsonFile file, string jsonPathInVar, List<JsonReference> references, List<Reference> missing)
    {
        Var = file.Var;
        Free = file.Free;
        References = references;
        Missing = missing;
        JsonPathInVar = jsonPathInVar;

        References.ForEach(t => t.FromJson = this);
        References.Select(t => t.Reference).Concat(missing).ToList().ForEach(t => t.FromJson = this);
        VarReferences = new HashSet<VarPackage>(References.Where(t => t.IsVarReference).Select(t => t.VarFile.ParentVar));
        FreeReferences = new HashSet<FreeFile>(References.Where(t => !t.IsVarReference).Select(t => t.File));

        if (file.IsVar)
        {
            Var.JsonFiles.Add(this);
            VarFile = Var.FilesDict[jsonPathInVar];
            VarFile.JsonFiles.Add(this);
        }
        else
        {
            Free.JsonFiles.Add(this);
        }
    }

    public override string ToString()
    {
        return IsVar ? (JsonPathInVar + " var: " + Var.Name.Filename) : Free.ToString();
    }
}