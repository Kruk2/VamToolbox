using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VamRepacker.Models;

namespace VamRepacker.Helpers
{
    public class JsonUpdateDto
    {
        public Reference ReferenceToUpdate { get; init; }
        public FileReferenceBase NewReference { get; set; }
    }

    public class JsonUpdater : IJsonUpdater
    {
        private readonly IFileSystem _fs;
        public bool DryRun { get; set; }

        public JsonUpdater(IFileSystem fs)
        {
            _fs = fs;
        }

        public async Task UpdateFreeJson(JsonFile json, IEnumerable<JsonUpdateDto> changes)
        {
            var jsonData = await ReadJson(json);
            ApplyFixes(jsonData, changes, json);

            if(!DryRun)
                await UpdateJson(json, jsonData);
        }

        public async Task UpdateVarJson(JsonFile json, IEnumerable<JsonUpdateDto> changes, ZipArchive archive, Dictionary<string, ZipArchiveEntry> entries)
        {
            var jsonData = await ReadJson(json, entries);
            ApplyFixes(jsonData, changes, json);

            if (!DryRun)
                await UpdateJson(json, jsonData, archive, entries);
        }

        private async Task UpdateJson(JsonFile json, IEnumerable<string> jsonData, ZipArchive varArchive, Dictionary<string, ZipArchiveEntry> entries)
        {
            var jsonEntry = entries[json.JsonPathInVar];
            var date = jsonEntry.LastWriteTime;
            jsonEntry.Delete();

            jsonEntry = varArchive.CreateEntry(json.JsonPathInVar, CompressionLevel.Optimal);
            entries[json.JsonPathInVar] = jsonEntry;

            {
                var stream = jsonEntry.Open();
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                foreach (var s in jsonData)
                {
                    await writer.WriteLineAsync(s);
                }
            }

            jsonEntry.LastWriteTime = date;
        }

        private async Task UpdateJson(JsonFile json, IEnumerable<string> jsonData)
        {
            await using var writer = new StreamWriter(json.Free.FullPath, false, Encoding.UTF8);
            foreach (var s in jsonData)
            {
                await writer.WriteLineAsync(s);
            }
        }

        private void ApplyFixes(IList<string> jsonData, IEnumerable<JsonUpdateDto> changes, JsonFile jsonFile)
        {
            int sum = 0;
            int i = 0;

            foreach (var updateDto in changes.OrderBy(t => t.ReferenceToUpdate.Index))
            {
                bool updated = false;
                for (; i < jsonData.Count; i++)
                {
                    sum += jsonData[i].Length;
                    if (sum > updateDto.ReferenceToUpdate.Index)
                    {
                        int start = updateDto.ReferenceToUpdate.Index - sum + jsonData[i].Length;
                        jsonData[i] = jsonData[i][..start] + BuildReference(updateDto.NewReference) + jsonData[i][(start + updateDto.ReferenceToUpdate.Length)..];
                        updated = true;
                        i++;
                        break;
                    }
                }

                if(!updated)
                    throw new InvalidOperationException($"Unable to find reference {updateDto.ReferenceToUpdate.FullLine} in json file {jsonFile}");
            }
        }

        private string BuildReference(FileReferenceBase file)
        {
            if (file is VarPackageFile varFile)
            {
                return $"{varFile.ParentVar.Name.Author}.{varFile.ParentVar.Name.Name}.latest:/{varFile.LocalPath}";
            }
            else
            {
                return $"{file.LocalPath}";
            }
        }

        private async Task<List<string>> ReadJson(JsonFile json)
        {
            using var reader = new StreamReader(json.Free.FullPath, Encoding.UTF8);
            return await ReadJsonInternal(reader);
        }

        private async Task<List<string>> ReadJson(JsonFile json,  Dictionary<string, ZipArchiveEntry> entries)
        {
            var jsonEntry = entries[json.JsonPathInVar];
            var stream = jsonEntry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await ReadJsonInternal(reader);
        }

        private async Task<List<string>> ReadJsonInternal(StreamReader reader)
        {
            var sb = new List<string>();
            while(!reader.EndOfStream)
            {
                sb.Add(await reader.ReadLineAsync());
            }

            return sb;
        }
    }

    public interface IJsonUpdater
    {
        Task UpdateFreeJson(JsonFile json, IEnumerable<JsonUpdateDto> changes);
        Task UpdateVarJson(JsonFile json, IEnumerable<JsonUpdateDto> changes, ZipArchive archive, Dictionary<string, ZipArchiveEntry> entries);
        bool DryRun { get; set; }
    }
}
