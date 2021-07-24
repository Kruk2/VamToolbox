using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using VamRepacker.Logging;
using VamRepacker.Models;

namespace VamRepacker.Helpers
{
    public interface IPresetGrouper
    {
        public Task GroupPresets<T>(List<T> files, VarPackageName varName, Func<string, Stream> openFileStream)
            where T : FileReferenceBase;
    }

    public class PresetGrouper :  IPresetGrouper
    {
        private readonly IFileSystem _fs;
        private readonly ILogger _logger;

        public PresetGrouper(IFileSystem fs, ILogger logger)
        {
            _fs = fs;
            _logger = logger;
        }

        public async Task GroupPresets<T>(List<T> files, VarPackageName varName, Func<string, Stream> openFileStream) 
            where T: FileReferenceBase
        {
            var grouped = files
                .Where(f => f.ExtLower is ".vaj" or ".vam" or ".vab")
                .Select(f => (basePath: f.LocalPath[..^f.ExtLower.Length], file: f))
                .GroupBy(x => x.basePath)
                .Select(g =>
                {
                    if (g.Count() is 1 or 2 or 3)
                    {
                        return (vaj: g.SingleOrDefault(f => f.file.ExtLower == ".vaj").file,
                            vam: g.SingleOrDefault(f => f.file.ExtLower == ".vam").file,
                            vab: g.SingleOrDefault(f => f.file.ExtLower == ".vab").file);
                    }

                    _logger.Log(
                        $"[MISSING-PRESET-FILE] Incorrect number of presets {g.Count()} {g.First().basePath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                    return default;
                });


            var filesMovedAsChildren = new HashSet<T>();
            foreach (var (vaj, vam, vab) in grouped)
            {
                var notNullPreset = vam ?? vaj ?? vab;
                if(notNullPreset == null)
                    continue;

                if (vam != null)
                {
                    (vam.InternalId, vam.VamAuthor) = await ReadVamInternalId(vam, openFileStream);
                }

                var localDir = _fs.Path.Combine(_fs.Path.GetDirectoryName(notNullPreset.LocalPath), _fs.Path.GetFileNameWithoutExtension(notNullPreset.LocalPath)).NormalizePathSeparators();
                if (vaj == null)
                {
                    _logger.Log($"[MISSING-PRESET-FILE] Missing vaj file for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                    notNullPreset.AddMissingChildren(localDir + ".vaj");
                }
                else if(notNullPreset != vaj)
                {
                    notNullPreset.AddChildren(vaj);
                    filesMovedAsChildren.Add(vaj);
                }

                if (vam == null)
                {
                    _logger.Log($"[MISSING-PRESET-FILE] Missing vam file for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                    notNullPreset.AddMissingChildren(localDir + ".vam");
                }
                else if(notNullPreset != vam)
                {
                    notNullPreset.AddChildren(vam);
                    filesMovedAsChildren.Add(vam);
                }

                if (vab == null)
                {
                    _logger.Log($"[MISSING-PRESET-FILE] Missing vab file for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                    notNullPreset.AddMissingChildren(localDir + ".vab");
                }
                else if(notNullPreset != vab)
                {
                    notNullPreset.AddChildren(vab);
                    filesMovedAsChildren.Add(vab);
                }
            }

            files.RemoveAll(t => filesMovedAsChildren.Contains(t));
        }

        private async Task<(string, string)> ReadVamInternalId<T>(T vam, Func<string, Stream> openFileStream) where T : FileReferenceBase
        {
            using var streamReader = new StreamReader(openFileStream(vam.LocalPath));
            string uuid = null;
            string author = null;

            while (!streamReader.EndOfStream)
            {
                var line = await streamReader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Contains("\"uid\""))
                {
                    uuid = line.Replace("\"uid\"", "");
                    uuid = uuid[(uuid.IndexOf("\"") + 1)..uuid.LastIndexOf("\"")];
                }

                if (line.Contains("\"creatorName\""))
                {
                    author = line.Replace("\"creatorName\"", "");
                    author = author[(author.IndexOf("\"") + 1)..author.LastIndexOf("\"")];
                }

                if (author != null && uuid != null)
                    return (uuid, author);
            }

            if(uuid is null)
                _logger.Log($"[MISSING-UUID-VAM] missing uuid in {vam.LocalPath} {(vam is VarPackageFile varFile ? varFile.ParentVar.Name : string.Empty)}");

            return (uuid, author);
        }
    }
}