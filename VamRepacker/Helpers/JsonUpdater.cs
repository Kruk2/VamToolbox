using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VamRepacker.Models;

namespace VamRepacker.Helpers;

public class JsonUpdateDto
{
    public JsonUpdateDto(Reference referenceToUpdate, FileReferenceBase newReference)
    {
        ReferenceToUpdate = referenceToUpdate;
        NewReference = newReference;
    }

    public Reference ReferenceToUpdate { get; init; }
    public FileReferenceBase NewReference { get; set; }
}

public sealed class JsonUpdater : IJsonUpdater
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

    private static async Task UpdateJson(JsonFile json, IEnumerable<string> jsonData, ZipArchive varArchive, Dictionary<string, ZipArchiveEntry> entries)
    {
        if(!json.File.IsVar) throw new ArgumentException($"Json file expected to be in var {json}", nameof(json));
        var jsonEntry = entries[json.File.LocalPath];
        var date = jsonEntry.LastWriteTime;
        jsonEntry.Delete();

        jsonEntry = varArchive.CreateEntry(json.File.LocalPath, CompressionLevel.Optimal);
        entries[json.File.LocalPath] = jsonEntry;

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

    private static async Task UpdateJson(JsonFile json, IEnumerable<string> jsonData)
    {
        if (json.File.IsVar) throw new ArgumentException($"Json file expected to be free file {json}", nameof(json));

        await using var writer = new StreamWriter(json.File.Free.FullPath, false, Encoding.UTF8);
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
                throw new InvalidOperationException($"Unable to find reference {updateDto.ReferenceToUpdate.Value} in json file {jsonFile}");
        }
    }

    private static string BuildReference(FileReferenceBase file)
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

    private static async Task<List<string>> ReadJson(JsonFile json)
    {
        if (json.File.IsVar) throw new ArgumentException($"Json file expected to free file {json}", nameof(json));

        using var reader = new StreamReader(json.File.Free.FullPath, Encoding.UTF8);
        return await ReadJsonInternal(reader);
    }

    private static async Task<List<string>> ReadJson(JsonFile json,  Dictionary<string, ZipArchiveEntry> entries)
    {
        if (!json.File.IsVar) throw new ArgumentException($"Json file expected to be in var {json}", nameof(json));

        var jsonEntry = entries[json.File.LocalPath];
        var stream = jsonEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await ReadJsonInternal(reader);
    }

    private static async Task<List<string>> ReadJsonInternal(StreamReader reader)
    {
        var sb = new List<string>();
        while(!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if(line is not null) sb.Add(line);
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