using System.Globalization;
using System.Runtime.CompilerServices;
using VamToolbox.Models;
using VamToolbox.Sqlite;

namespace VamToolbox.Helpers;

public sealed class Reference
{
    public string NormalizedLocalPath => Value.Split(':').Last().NormalizeAssetPath();
    public string Value { get; }
    public int Index { get; }
    public int Length { get; }

    // these are read from next line in JSON file
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }

    public override string ToString() => $"{Value} at index {Index}";

    private string? _estimatedReferenceLocation;

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

    private string? _estimatedExtension;
    public string EstimatedExtension => _estimatedExtension ??= '.' + Value.Split('.').Last().ToLower(CultureInfo.InvariantCulture);
    private AssetType? _estimatedAssetType;
    public AssetType EstimatedAssetType => _estimatedAssetType ??= EstimatedExtension.ClassifyType(EstimatedReferenceLocation); 
    public string EstimatedReferenceLocation => _estimatedReferenceLocation ??= Value.Split(':').Last().NormalizeAssetPath();
    public FileReferenceBase ForJsonFile { get; internal set; }

    private bool _estimatedVarNameCalculated;
    private VarPackageName? _estimatedVarName;
    public VarPackageName? EstimatedVarName => GetEstimatedVarName();

    private VarPackageName? GetEstimatedVarName()
    {
        if (_estimatedVarNameCalculated) return _estimatedVarName;

        _estimatedVarNameCalculated = true;
        if(Value.StartsWith("SELF:", StringComparison.OrdinalIgnoreCase) || !Value.Contains(':')) return null;
        var name = Value.Split(':').First();
        VarPackageName.TryGet(name + ".var", out _estimatedVarName);
        return _estimatedVarName;
    }
}

public interface IJsonFileParser
{
    public Reference? GetAsset(ReadOnlySpan<char> line, int offset, FileReferenceBase fromFile, out string? outputError);
}

public sealed class JsonScannerHelper : IJsonFileParser
{
    private static readonly HashSet<int> Extensions = new[]{
        "vmi", "vam", "vaj", "vap", "jpg", "jpeg", "tif", "png", "mp3", "ogg", "wav", "assetbundle", "scene",
        "cs", "cslist", "tiff", "dll", ".audiobundle"
    }.Select(t => string.GetHashCode(t, StringComparison.OrdinalIgnoreCase)).ToHashSet();

    //public static readonly ConcurrentDictionary<string, string> SeenExtensions = new();

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Reference? GetAsset(ReadOnlySpan<char> line, int offset, FileReferenceBase fromFile, out string? outputError)
    {
        outputError = null;
        var lastQuoteIndex = line.LastIndexOf('"');
        if (lastQuoteIndex == -1)
            return null;

        var prevQuoteIndex = line[..lastQuoteIndex].LastIndexOf('"');
        if (prevQuoteIndex == -1)
            return null;

        var okToParse = false;
        if (prevQuoteIndex - 3 >= 0 && line[prevQuoteIndex - 1] == ' ')
        {
            if (line[prevQuoteIndex - 2] == ':')
            {
                // '" : ' OR '": '
                if (line[prevQuoteIndex - 3] == '"' || (prevQuoteIndex - 4 >= 0 && line[prevQuoteIndex - 3] == ' ' && line[prevQuoteIndex - 4] == '"'))
                    okToParse = true;
            }
        }
        else if (prevQuoteIndex - 2 >= 0 && line[prevQuoteIndex - 1] == ':')
        {
            // '":' OR '" :'
            if (line[prevQuoteIndex - 2] == '"' || (prevQuoteIndex - 3 >= 0 && line[prevQuoteIndex - 2] == ' ' && line[prevQuoteIndex - 3] == '"'))
                okToParse = true;
        }

        if (!okToParse)
            return null;

        var assetName = line[(prevQuoteIndex + 1)..lastQuoteIndex];
        var lastDot = assetName.LastIndexOf('.');
        if (lastDot == -1 || lastDot == assetName.Length - 1)
            return null;
        var assetExtension = assetName[^(assetName.Length - lastDot - 1)..];
        //var ext = assetExtension.ToString();
        //SeenExtensions.GetOrAdd(ext, ext);

        var endsWithExtension = Extensions.Contains(string.GetHashCode(assetExtension, StringComparison.OrdinalIgnoreCase));
        if (!endsWithExtension || !IsUrl(assetName, line, ref outputError))
            return null;

        return new Reference(assetName.ToString(), index: offset + prevQuoteIndex + 1, length: lastQuoteIndex - prevQuoteIndex - 1, fromFile);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool IsUrl(ReadOnlySpan<char> reference, ReadOnlySpan<char> line, ref string? error)
    {
        const StringComparison c = StringComparison.OrdinalIgnoreCase;

        if (reference.StartsWith("http://") || reference.StartsWith("https://"))
            return false;

        bool isURL;
        if (reference.Contains("\"simTexture\"", c))
        {
            return false;
        }
        else if (reference.EndsWith(".vam", c))
        {
            isURL = line.Contains("\"id\"", c);
        }
        else if (reference.EndsWith(".vap", c))
        {
            isURL = line.Contains("\"presetFilePath\"", c);
        }
        else if (reference.EndsWith(".vmi", c))
        {
            isURL = line.Contains("\"uid\"", c);
        }
        else
        {
            isURL = line.Contains("tex\"", c) || line.Contains("texture\"", c) || line.Contains("url\"", c) ||
                    line.Contains("bumpmap\"", c) || line.Contains("\"url", c) || line.Contains("LUT\"", c) ||
                    line.Contains("\"plugin#", c);
        }

        if (!isURL)
        {
            if (line.Contains("\"displayName\"", c) || line.Contains("\"audioClip\"", c) ||
                line.Contains("\"selected\"", c) || line.Contains("\"audio\"", c))
            {
                return false;
            }

            error = string.Concat("Invalid type in json scanner: ", line);
            return false;
            //throw new VamToolboxException("Invalid type in json scanner: " + line);
        }

        return true;
    }
}