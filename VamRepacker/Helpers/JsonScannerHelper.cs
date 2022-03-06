using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using VamRepacker.Sqlite;

namespace VamRepacker.Helpers
{
    public class Reference : IEquatable<Reference>
    {
        public bool Equals(Reference other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Reference) obj);
        }

        public override int GetHashCode() => Value.GetHashCode();


        public string NormalizedLocalPath => Value.Split(':').Last().NormalizePathSeparators();
        public string Value { get; init; }
        public int Index { get; init; }
        public int Length { get; init; }

        // these are read from next line in JSON file
        public string MorphName { get; set; }
        public string InternalId { get; set; }

        public override string ToString() => $"{Value} at index {Index}";

        private string _estimatedReferenceLocation;

        public Reference()
        {
        }

        public Reference(ReferenceEntry referenceEntry)
        {
            Value = referenceEntry.Value;
            InternalId = referenceEntry.InternalId;
            MorphName = referenceEntry.MorphName;
            Index = referenceEntry.Index;
            Length = referenceEntry.Length;
        }

        public string EstimatedReferenceLocation => _estimatedReferenceLocation ??= GetEstimatedReference();
        public string EstimatedVarName => Value.StartsWith("SELF:") || !Value.Contains(':') ? null : Value.Split(':').First();

        private string GetEstimatedReference()
        {
            if (Value.StartsWith("SELF:") || !Value.Contains(':'))
                return Value.Split(':').Last().NormalizePathSeparators();
            return Value.Split(':')[0].NormalizePathSeparators();
        }
    }

    public interface IJsonFileParser
    {
        public Reference GetAsset(string line, int offset, out string error);
    }

    public class JsonScannerHelper : IJsonFileParser
    {
        private static readonly HashSet<int> Extensions = new[]{
            "vmi", "vam", "vaj", "vap", "jpg", "jpeg", "tif", "png", "mp3", "ogg", "wav", "assetbundle", "scene",
            "cs", "cslist", "tiff", "dll"
        }.Select(t => string.GetHashCode(t, StringComparison.OrdinalIgnoreCase)).ToHashSet();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public Reference GetAsset(string l, int offset, out string error)
        {
            error = null;
            var line = l.AsSpan();
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

            var endsWithExtension = Extensions.Contains(string.GetHashCode(assetExtension, StringComparison.OrdinalIgnoreCase));
            if (!endsWithExtension || !IsUrl(ref assetName, l, ref error))
                return null;


            return new Reference
            {
                Value = assetName.ToString(),
                Index = offset + prevQuoteIndex + 1,
                Length = lastQuoteIndex - prevQuoteIndex - 1
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static bool IsUrl(ref ReadOnlySpan<char> reference, string line, ref string error)
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
                isURL = line.Contains("\"id\"");
            }
            else if (reference.EndsWith(".vap", c))
            {
                isURL = line.Contains("\"presetFilePath\"");
            }
            else if (reference.EndsWith(".vmi", c))
            {
                isURL = line.Contains("\"uid\"");
            }
            else
            {
                isURL = line.Contains("tex\"", c) || line.Contains("texture\"", c) || line.Contains("url\"", c) ||
                        line.Contains("bumpmap\"", c) || line.Contains("\"url", c) || line.Contains("LUT\"") ||
                        line.Contains("\"plugin#");
            }

            if (!isURL)
            {
                if (line.Contains("\"displayName\"") || line.Contains("\"audioClip\"") ||
                    line.Contains("\"selected\"") || line.Contains("\"audio\""))
                {
                    return false;
                }

                error = "Invalid type in json scanner: " + line;
                return false;
                //throw new VamRepackerException("Invalid type in json scanner: " + line);
            }

            return true;
        }
    }
}