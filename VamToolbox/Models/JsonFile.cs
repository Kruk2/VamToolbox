using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class JsonFile
{
    private readonly List<JsonReference> _references = new();
    private readonly List<Reference> _missing = new();

    public FileReferenceBase File { get; }
    public IReadOnlyCollection<JsonReference> References => _references;
    public IReadOnlyCollection<Reference> Missing => _missing;

    public IReadOnlySet<VarPackage> VarReferences { get; } = new HashSet<VarPackage>();
    public IReadOnlySet<FreeFile> FreeReferences { get; } = new HashSet<FreeFile>();

    public JsonFile(OpenedPotentialJson openedJsonFile)
    {
        File = openedJsonFile.File;
    }

    public void AddReference(JsonReference reference)
    {
        _references.Add(reference);
        if (reference.IsVarReference)
            ((HashSet<VarPackage>)VarReferences).Add(reference.ParentVar);
        else
            ((HashSet<FreeFile>)FreeReferences).Add(reference.FreeFile);

        if(reference.File != File)
            reference.File.UsedByJsonFiles[this] = true;
    }
    public void AddMissingReference(Reference reference) => _missing.Add(reference);

    public override string ToString() => File.ToString();


}