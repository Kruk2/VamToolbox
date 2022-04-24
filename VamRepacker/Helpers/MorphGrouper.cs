using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using VamRepacker.Logging;
using VamRepacker.Models;

namespace VamRepacker.Helpers;

public interface IMorphGrouper
{
    public Task GroupMorphsVmi<T>(List<T> files, VarPackageName? varName, Func<string, Stream> openFileStream, ILookup<string, (string, FileReferenceBase)> favMorphs)
        where T : FileReferenceBase;
}

public sealed class MorphGrouper : IMorphGrouper
{
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;

    public MorphGrouper(IFileSystem fs, ILogger logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task GroupMorphsVmi<T>(List<T> files, VarPackageName? varName, Func<string, Stream> openFileStream, ILookup<string, (string, FileReferenceBase)> favMorphs)
        where T : FileReferenceBase
    {
        var filesMovedAsChildren = new HashSet<T>();
        var pairs = GroupMorphs(files, favMorphs);
        foreach (var (vmi, vmb, fav) in pairs)
        {
            if (vmi?.FilenameLower == "nose bridge thin.vmi")
                Debug.Write(true);
            var notNullPreset = vmi ?? vmb;
            if(notNullPreset == null)
                continue;

            if (vmi is not null)
            {
                vmi.MorphName = await ReadVmiName(vmi, openFileStream);
            }

            var localDir = _fs.Path.Combine(_fs.Path.GetDirectoryName(notNullPreset.LocalPath), _fs.Path.GetFileNameWithoutExtension(notNullPreset.LocalPath)).NormalizePathSeparators();
            if (vmi == null)
            {
                _logger.Log($"[MISSING-MORPH-FILE] Missing vmi file for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                notNullPreset.AddMissingChildren(localDir + ".vmi");
            }
            else if(notNullPreset != vmi)
            {
                notNullPreset.AddChildren(vmi);
                filesMovedAsChildren.Add(vmi);
            }

            if (vmb == null)
            {
                _logger.Log($"[MISSING-MORPH-FILE] Missing vmb file for {notNullPreset.LocalPath}{(varName != null ? $" in var {varName.Filename}" : "")}");
                notNullPreset.AddMissingChildren(localDir + ".vmb");
            }
            else if(notNullPreset != vmb)
            {
                notNullPreset.AddChildren(vmb);
                filesMovedAsChildren.Add(vmb);
            }

            if (fav != null)
            {
                notNullPreset.AddChildren(fav);
                filesMovedAsChildren.Add((T)fav);
            }
        }

        files.RemoveAll(t => filesMovedAsChildren.Contains(t));
    }

    private IEnumerable<(T?, T?, FileReferenceBase?)> GroupMorphs<T>(IEnumerable<T> files, ILookup<string, (string basePath, FileReferenceBase file)> favs) where T: FileReferenceBase
    {
        return files
            .Where(f => f.ExtLower is ".vmi" or ".vmb")
            .Select(f => (basePath: f.LocalPath[..^f.ExtLower.Length], file: f))
            .GroupBy(x => x.basePath)
            .Select(g =>
            {
                if (g.Count() is 1 or 2)
                {
                    var firstFile = g.First().file;
                    var fav = favs[firstFile.FilenameWithoutExt]
                        .FirstOrDefault(t => firstFile.LocalPath.StartsWith(t.basePath, StringComparison.Ordinal));
                    return (vmi: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vmi").file,
                        vmb: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vmb").file,
                        fav: (FileReferenceBase?)fav.file);
                }

                _logger.Log($"[MISSING-MORPH-FILE] Incorrect number of morphs {g.Count()} {g.First().basePath}");
                return default;
            });
    }

    private async Task<string?> ReadVmiName<T>(T vam, Func<string, Stream> openFileStream) where T : FileReferenceBase
    {
        using var streamReader = new StreamReader(openFileStream(vam.LocalPath));

        while (!streamReader.EndOfStream)
        {
            var line = await streamReader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"displayName\"")) continue;

            var uuid = line.Replace("\"displayName\"", "");
            return uuid[(uuid.IndexOf('\"') + 1)..uuid.LastIndexOf('\"')];
        }

        _logger.Log($"[MISSING-DISPLAYNAME-VMI] missing name in {vam.LocalPath} {(vam is VarPackageFile varFile ? varFile.ParentVar.Name : string.Empty)}");
        return null;
    }
}