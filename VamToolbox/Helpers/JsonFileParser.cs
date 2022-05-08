using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using VamToolbox.Models;

namespace VamToolbox.Helpers;

public interface IJsonFileParser
{
    public Reference? GetAsset(ReadOnlySpan<char> line, int offset, FileReferenceBase fromFile, out string? outputError);
}

public sealed class JsonFileParser : IJsonFileParser
{
    private static readonly HashSet<int> Extensions = new[]{
        "vmi", "vam", "vaj", "vap", "jpg", "jpeg", "tif", "png", "mp3", "ogg", "wav", "assetbundle", "scene",
        "cs", "cslist", "tiff", "dll", "audiobundle", "voicebundle", "json"
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
        if (prevQuoteIndex - 3 >= 0 && line[prevQuoteIndex - 1] == ' ') {
            if (line[prevQuoteIndex - 2] == ':') {
                // '" : ' OR '": '
                if (line[prevQuoteIndex - 3] == '"' || (prevQuoteIndex - 4 >= 0 && line[prevQuoteIndex - 3] == ' ' && line[prevQuoteIndex - 4] == '"'))
                    okToParse = true;
            }
        } else if (prevQuoteIndex - 2 >= 0 && line[prevQuoteIndex - 1] == ':') {
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
        //if (ext.All(char.IsLetterOrDigit) && !ext.All(char.IsDigit))
        //    SeenExtensions.GetOrAdd(ext, ext);

        var endsWithExtension = Extensions.Contains(string.GetHashCode(assetExtension, StringComparison.OrdinalIgnoreCase));
        if (!endsWithExtension || !IsUrl(assetName, line, assetExtension, fromFile, ref outputError))
            return null;


        return new Reference(assetName.ToString(), index: offset + prevQuoteIndex + 1, length: lastQuoteIndex - prevQuoteIndex - 1, fromFile);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool IsUrl(ReadOnlySpan<char> reference, ReadOnlySpan<char> line, ReadOnlySpan<char> ext, FileReferenceBase fromFile, ref string? error)
    {
        const StringComparison c = StringComparison.OrdinalIgnoreCase;

        if (reference.StartsWith("http://") || reference.StartsWith("https://"))
            return false;

        // TODO handle assets for scripts better, maybe some kind of static mapping for more popular ones?
        if (fromFile.ExtLower == ".json") {
            if(ext.Equals("wav", c) || ext.Equals("mp3", c) || ext.Equals("ogg", c)) {
                if ((fromFile.LocalPath.Contains("VAMMoan", c) || line.Contains("VAMMoan", c)) && line.Contains("\"audio\"", c)) {
                    return false;
                }
                if ((fromFile.LocalPath.Contains("VAMDeluxe", c) || line.Contains("VAMDeluxe", c)) && line.Contains("\"audio\"", c)) {
                    return false;
                }
            }

            //cotyounoyume.ExpressionBlushingAndTears
            if (ext.Equals("png", c) && line.Contains("\"File\"", c) && fromFile.LocalPath.Contains("ExpressionBlushingAndTears", c)) {
                return false;
            }
        }

        if (fromFile.ExtLower == ".uiap" && line.Contains("filePath\"", c)) {
            return true;
        }

        if (ext.Equals("vam", c) && (line.Contains("\"id\"", c) || line.Contains("\"receiverTargetName\"", c))) {
            return true;
        }
        if (ext.Equals("vmi", c) && line.Contains("\"uid\"", c)) {
            return true;
        }
        if ((ext.Equals("cs", c) || ext.Equals("cslist", c) || ext.Equals("dll", c)) &&
            (line.Contains("\"plugin#", c) || line.Contains("\"assetDllUrl\"", c))) {
            return true;
        }
        if (ext.Equals("vap", c) && line.Contains("\"presetFilePath\"", c)) {
            return true;
        }
        if (ext.Equals("json", c)) {
            if (line.Contains("\"storePath\"", c) || line.Contains("\"sceneFilePath\"", c) ||
                line.Contains("\"presetFilePath\"", c) || line.Contains("\"Additional Button Scene\"", c)) {
                return true;
            }

            if (line.Contains("\"expression_", c) || line.Contains("\"receiverTargetName\"", c)) {
                return false;
            }
        }
        if ((ext.Equals("png", c) || ext.Equals("jpg", c) || ext.Equals("jpeg", c) || ext.Equals("tiff", c) || ext.Equals("tif", c)) &&
            (line.Contains("\"simTexture\"", c) || line.Contains("\"customTexture_", c) || 
             line.Contains("Url\"", c) || line.Contains("\"urlValue\"", c) || line.Contains("\"Path\"", c) || line.Contains("\"File\"", c) ||
             line.Contains("\"Spectral LUT\"", c) || line.Contains("\"Light Texture\"", c) ||
             line.Contains("Subdermis Texture\"", c) || line.Contains("\"UserLUT\"", c) || line.Contains("\"LensDirt Texture\"", c))) {
            return true;
        }
        if (ext.Equals("wav", c) || ext.Equals("mp3", c) || ext.Equals("ogg", c)) {
            if (line.Contains("\"sourcePath\"", c) || line.Contains("\"url\"", c)) {
                return true;
            }
            if (line.Contains("\"uid\"", c) || line.Contains("\"displayName\"", c) || 
                line.Contains("\"audioClip\"", c) || line.Contains("\"sourceClip\"", c) ||
                line.Contains("\"stringValue\"", c) || line.Contains("\"clip_", c) ||
                line.Contains("\"selected\"", c) || line.Contains("\"receiverTargetName\"", c) ||
                line.Contains("\"backgroundMusicClip\"", c) || line.Contains("\"Audio Clips\"", c) ||
                line.Contains("\"Action1\\nAudio", c) ||
                (line.Contains("\"act", c) && line.Contains("Target", c) && line.Contains("ValueName\"", c))) {
                return false;
            }
        }

        if (ext.Equals("audiobundle", c) && (line.Contains("\"AudioBundle\"", c) || line.Contains("\"AssetBundle\"", c))) {
            return true;
        }
        if (ext.Equals("assetbundle", c)) {
            return true;
        }
        if (ext.Equals("scene", c) && line.Contains("\"assetUrl\"", c)) {
            return true;
        }

        error = string.Concat("Invalid type in json scanner: ", line, " in ", fromFile.ToString());
        return false;
    }
}