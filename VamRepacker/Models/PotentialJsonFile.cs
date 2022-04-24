using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models;

public sealed class PotentialJsonFile : IDisposable
{
    [MemberNotNullWhen(true, nameof(Var))]
    [MemberNotNullWhen(false, nameof(Free))]
    public bool IsVar => Var != null;
    public VarPackage? Var { get; }
    public FreeFile? Free { get; }

    public string Name => IsVar ? Var.Name.Filename : Free.LocalPath;

    private FileStream? _varFileStream;
    private ZipArchive? _varArchive;

    private readonly Dictionary<string, List<Reference>> _varFilesReferenceCache = new(StringComparer.Ordinal);
    private List<Reference>? _freeFileReferenceCache;
    public PotentialJsonFile(VarPackage var) => Var = var;
    public PotentialJsonFile(FreeFile free) => Free = free;

    public IEnumerable<OpenedPotentialJson> OpenJsons()
    {
        if (IsVar)
        {
            var potentialJsonFiles = Var.Files
                .SelectMany(t => t.SelfAndChildren())
                .Where(t => t.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(t.ExtLower));
            IDictionary<string, ZipArchiveEntry>? entries = null;

            foreach (var potentialJsonFile in potentialJsonFiles)
            {
                if (_varFilesReferenceCache.ContainsKey(potentialJsonFile.LocalPath))
                {
                    yield return new OpenedPotentialJson(potentialJsonFile) { CachedReferences = _varFilesReferenceCache[potentialJsonFile.LocalPath] };
                }
                else
                {
                    if (!potentialJsonFile.Dirty) throw new InvalidOperationException($"Tried to read not-dirty var file {potentialJsonFile}");
                    _varFileStream ??= File.OpenRead(Var.FullPath);
                    _varArchive ??= new ZipArchive(_varFileStream);
                    entries ??=_varArchive.Entries.ToDictionary(t => t.FullName.NormalizePathSeparators());

                    yield return new OpenedPotentialJson(potentialJsonFile) { Stream = entries[potentialJsonFile.LocalPath].Open() };
                }
            }
        }
        else
        {
            if (_freeFileReferenceCache is not null)
            {
                yield return new OpenedPotentialJson(Free) { CachedReferences = _freeFileReferenceCache };
            }
            else if (KnownNames.IsPotentialJsonFile(Free.ExtLower))
            {
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