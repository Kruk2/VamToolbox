using System.Diagnostics.CodeAnalysis;
using Ionic.Zip;
using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class PotentialJsonFile : IDisposable
{
    [MemberNotNullWhen(true, nameof(Var))]
    [MemberNotNullWhen(false, nameof(Free))]
    public bool IsVar => Var != null;
    public VarPackage? Var { get; }
    public FreeFile? Free { get; }

    public string Name => IsVar ? Var.Name.Filename : Free.LocalPath;

    private FileStream? _varFileStream;
    private ZipFile? _varArchive;

    private readonly Dictionary<string, List<Reference>> _varFilesReferenceCache = new(StringComparer.Ordinal);
    private List<Reference>? _freeFileReferenceCache;
    public PotentialJsonFile(VarPackage var) => Var = var;
    public PotentialJsonFile(FreeFile free) => Free = free;

    public IEnumerable<OpenedPotentialJson> OpenJsons()
    {
        if (IsVar) {
            var potentialJsonFiles = Var.Files
                .SelfAndChildren()
                .Where(t => t.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(t.ExtLower));
            IDictionary<string, ZipEntry>? entries = null;

            foreach (var potentialJsonFile in potentialJsonFiles) {
                if (_varFilesReferenceCache.ContainsKey(potentialJsonFile.LocalPath)) {
                    yield return new OpenedPotentialJson(potentialJsonFile) { CachedReferences = _varFilesReferenceCache[potentialJsonFile.LocalPath] };
                } else {
                    if (!potentialJsonFile.Dirty) throw new InvalidOperationException($"Tried to read not-dirty var file {potentialJsonFile}");
                    _varFileStream ??= File.OpenRead(Var.FullPath);
                    _varArchive ??= ZipFile.Read(_varFileStream);
                    _varArchive.CaseSensitiveRetrieval = true;
                    entries ??= _varArchive.Entries.Where(t => !t.IsDirectory).ToDictionary(t => t.FileName.NormalizePathSeparators());

                    yield return new OpenedPotentialJson(potentialJsonFile) { Stream = entries[potentialJsonFile.LocalPath].OpenReader() };
                }
            }
        } else {
            if (_freeFileReferenceCache is not null) {
                yield return new OpenedPotentialJson(Free) { CachedReferences = _freeFileReferenceCache };
            } else if (KnownNames.IsPotentialJsonFile(Free.ExtLower)) {
                if (!Free.Dirty) throw new InvalidOperationException($"Tried to read not-dirty file {Free}");
                yield return new OpenedPotentialJson(Free) { Stream = File.OpenRead(Free.FullPath) };
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
        if (!IsVar) throw new InvalidOperationException("Unable to add cache for non-var file");
        _varFilesReferenceCache[fileLocalPath] = references;
    }

    public void AddCachedReferences(List<Reference> references)
    {
        if (IsVar) throw new InvalidOperationException("Unable to add cache for var file");
        _freeFileReferenceCache = references;
    }
}

public class OpenedPotentialJson
{
    public OpenedPotentialJson(FileReferenceBase file)
    {
        File = file;
    }

    public Stream? Stream { get; init; }
    public FileReferenceBase File { get; init; }
    public List<Reference>? CachedReferences { get; init; }
}