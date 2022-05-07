using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public interface IPresetGrouper
{
    public Task GroupPresets<T>(List<T> files, VarPackageName? varName, Func<string, Stream> openFileStream)
        where T : FileReferenceBase;
}

public sealed class PresetGrouper : IPresetGrouper
{
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;

    public PresetGrouper(IFileSystem fs, ILogger logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task GroupPresets<T>(List<T> files, VarPackageName? varName, Func<string, Stream> openFileStream)
        where T : FileReferenceBase
    {
        var grouped = files
            .Where(f => f.ExtLower is ".vaj" or ".vam" or ".vab")
            .Select(f => (basePath: f.LocalPath[..^f.ExtLower.Length], file: f))
            .GroupBy(x => x.basePath)
            .Select(g => {
                if (g.Count() is 1 or 2 or 3) {
                    return (vaj: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vaj").file,
                        vam: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vam").file,
                        vab: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vab").file);
                }

                _logger.Log(
                    $"[MISSING-PRESET-FILE] Incorrect number of presets {g.Count()} {g.First().basePath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                return default;
            });


        var filesMovedAsChildren = new HashSet<T>();
        foreach (var (vaj, vam, vab) in grouped) {
            var notNullPreset = vam ?? vaj ?? vab;
            if (notNullPreset == null)
                continue;

            if (vam is { InternalId: null }) {
                vam.InternalId = await ReadVamInternalId(vam, openFileStream);
            }

            var localDir = _fs.Path.Combine(_fs.Path.GetDirectoryName(notNullPreset.LocalPath), _fs.Path.GetFileNameWithoutExtension(notNullPreset.LocalPath)).NormalizePathSeparators();
            if (vaj == null) {
                _logger.Log($"[MISSING-PRESET-FILE] Missing vaj toFile for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                notNullPreset.AddMissingChildren(localDir + ".vaj");
            } else if (notNullPreset != vaj) {
                notNullPreset.AddChildren(vaj);
                filesMovedAsChildren.Add(vaj);
            }

            if (vam == null) {
                _logger.Log($"[MISSING-PRESET-FILE] Missing vam toFile for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                notNullPreset.AddMissingChildren(localDir + ".vam");
            } else if (notNullPreset != vam) {
                notNullPreset.AddChildren(vam);
                filesMovedAsChildren.Add(vam);
            }

            if (vab == null) {
                _logger.Log($"[MISSING-PRESET-FILE] Missing vab toFile for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                notNullPreset.AddMissingChildren(localDir + ".vab");
            } else if (notNullPreset != vab) {
                notNullPreset.AddChildren(vab);
                filesMovedAsChildren.Add(vab);
            }
        }

        files.RemoveAll(t => filesMovedAsChildren.Contains(t));
    }

    private async Task<string?> ReadVamInternalId<T>(T vam, Func<string, Stream> openFileStream) where T : FileReferenceBase
    {
        await using var streamReader = openFileStream(vam.LocalPath);
        try {
            var reader = await JsonSerializer.DeserializeAsync<VamFile>(streamReader, new JsonSerializerOptions {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (!string.IsNullOrWhiteSpace(reader?.Uid)) {
                return reader.Uid;
            }
        } catch (Exception ex) {
            _logger.Log(ex.ToString());
        }

        _logger.Log($"[MISSING-UUID-VAM] missing uuid in {vam}");

        return null;
    }

    private class VamFile
    {
        [JsonPropertyName("uid")]
        public string Uid { get; set; } = null!;
    }

}