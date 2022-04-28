using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using VamToolbox.Hashing;
using VamToolbox.Helpers;

namespace VamToolbox.Models;

public abstract class FileReferenceBase
{
    public string LocalPath { get; }
    public string FilenameLower { get; }
    public string FilenameWithoutExt { get; }
    public string ExtLower { get; }


    public string? InternalId { get; internal set; }
    public string? MorphName { get; internal set; }

    [MemberNotNullWhen(true, nameof(VarFile))]
    [MemberNotNullWhen(true, nameof(Var))]
    [MemberNotNullWhen(false, nameof(Free))]
    public bool IsVar => this is VarPackageFile;
    public VarPackage? Var => this is VarPackageFile varFile ? varFile.ParentVar : null;
    public VarPackageFile? VarFile => this as VarPackageFile;
    public FreeFile? Free => this as FreeFile;

    public long Size { get; }
    public bool IsInVaMDir { get; }
    public AssetType Type { get; } = AssetType.Unknown;
    public FileReferenceBase? ParentFile { get; protected internal set; }
    public bool Dirty { get; set; }
    public DateTime ModifiedTimestamp { get; }

    public JsonFile? JsonFile { get; internal set; }
    public ConcurrentDictionary<JsonFile, bool> UsedByJsonFiles { get; } = new();
    public int UsedByVarPackagesOrFreeFilesCount => UsedByJsonFiles.Keys.Select(t => t.File.IsVar ? (object)t.File.Var : t.File.Free).Distinct().Count();
    public abstract IReadOnlyCollection<FileReferenceBase> Children { get; }
    public List<string> MissingChildren { get; } = new();

    public string? Hash { get; internal set; }

    private string? _hashWithChildren;
    public string HashWithChildren => _hashWithChildren ??= MD5Helper.GetHash(Hash!, Children.Select(t => t.Hash!));
    public long SizeWithChildren => Size + Children.Sum(t => t.SizeWithChildren);

    protected FileReferenceBase(string localPath, long size, bool isInVamDir, DateTime modifiedTimestamp)
    {
        LocalPath = localPath.NormalizePathSeparators();
        FilenameLower = Path.GetFileName(localPath).ToLowerInvariant();
        FilenameWithoutExt = Path.GetFileNameWithoutExtension(localPath);
        ExtLower = Path.GetExtension(FilenameLower);
        Size = size;
        IsInVaMDir = isInVamDir;
        ModifiedTimestamp = modifiedTimestamp;

        if (ExtLower is ".vmi" or ".vmb") Type = AssetType.Morph;
    }

    public override string ToString() => LocalPath;

    public abstract void AddChildren(FileReferenceBase children);

    public void AddMissingChildren(string localChildrenPath) => MissingChildren.Add(localChildrenPath);
}