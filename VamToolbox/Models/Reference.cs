using System.Globalization;
using VamToolbox.Sqlite;
using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class Reference
{
    public string Value { get; }
    public int Index { get; }
    public int Length { get; }

    // these are read from next line in JSON file
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }

    public FileReferenceBase ForJsonFile { get; internal set; }
    public override string ToString() => $"{Value} at index {Index}";

    public Reference(string value, int index, int length, FileReferenceBase forJsonFile)
    {
        Value = value;
        Index = index;
        Length = length;
        ForJsonFile = forJsonFile;
    }

    public Reference(ReferenceEntry referenceEntry, FileReferenceBase forJsonFile)
    {
        Value = referenceEntry.Value!;
        InternalId = referenceEntry.InternalId;
        MorphName = referenceEntry.MorphName;
        Index = referenceEntry.Index;
        Length = referenceEntry.Length;
        ForJsonFile = forJsonFile;
    }

    private bool? _hasSelfKeyword;
    public bool HasSelfKeyword => _hasSelfKeyword ??= Value.StartsWith("SELF:", StringComparison.OrdinalIgnoreCase);
    private bool? _containsSemiColon;
    public bool HasSemiColon => _containsSemiColon ??= Value.Contains(':');

    private string? _estimatedExtension;
    public string EstimatedExtension => _estimatedExtension ??= '.' + Value.Split('.').Last().ToLower(CultureInfo.InvariantCulture);
    private AssetType? _estimatedAssetType;
    public AssetType EstimatedAssetType => _estimatedAssetType ??= EstimatedExtension.ClassifyType(EstimatedReferenceLocation);
    private string? _estimatedReferenceLocation;
    public string EstimatedReferenceLocation => _estimatedReferenceLocation ??= Value.Split(':').Last().NormalizeAssetPath();

    private bool _estimatedVarNameCalculated;
    private VarPackageName? _estimatedVarName;
    public VarPackageName? EstimatedVarName => GetEstimatedVarName();

    private VarPackageName? GetEstimatedVarName()
    {
        if (_estimatedVarNameCalculated) return _estimatedVarName;

        _estimatedVarNameCalculated = true;
        if (HasSelfKeyword || !HasSemiColon) return null;

        string? varName = null;
        var refPathSplit = Value.Split(':');
        if (refPathSplit[0].StartsWith("AddonPackages/", StringComparison.OrdinalIgnoreCase)) {
            varName = refPathSplit[0].Replace("AddonPackages/", "");
        } else if (refPathSplit.Length == 3) {
            if (refPathSplit[0].Equals("clothing", StringComparison.OrdinalIgnoreCase) ||
                refPathSplit[0].Equals("toggle", StringComparison.OrdinalIgnoreCase)) {
                varName = refPathSplit[1];
            }
        } else {
            varName = refPathSplit[0];
        }

        if (varName is null) {
            return null;
        }

        if (!varName.EndsWith(".var", StringComparison.OrdinalIgnoreCase)) {
            varName += ".var";
        }

        VarPackageName.TryGet(varName, out _estimatedVarName);
        return _estimatedVarName;
    }
}