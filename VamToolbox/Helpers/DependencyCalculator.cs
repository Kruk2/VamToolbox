using MoreLinq;
using VamToolbox.Models;

namespace VamToolbox.Helpers;

public static class DependencyCalculator
{
    public static (List<VarPackage>, List<FreeFile>) CalculateTrimmedDeps(List<JsonFile> jsonFiles)
    {
        var queue = jsonFiles.SelectMany(t => t.References).Distinct().ToList();
        var processedFiles = new HashSet<FileReferenceBase>(jsonFiles.Select(t => t.File));
        var varDeps = new HashSet<VarPackage>();
        var freeFileDeps = new HashSet<FreeFile>();

        while (queue.Count > 0)
        {
            var item = queue[^1];
            queue.RemoveAt(queue.Count - 1);

            var ext = item.File.ExtLower;
            if (item.IsVarReference)
                varDeps.Add(item.ParentVar);
            else
                freeFileDeps.Add(item.FreeFile);

            if (!KnownNames.ExtReferencesToPresets.Contains(ext))
                continue;

            if (processedFiles.Contains(item.File))
                continue;

            if(item.File.JsonFile is not null)
                queue.AddRange(item.File.JsonFile.References);
            processedFiles.Add(item.File);
        }

        return (varDeps.ToList(), freeFileDeps.ToList());
    }

    public static (List<VarPackage>, List<FreeFile>) CalculateAllVarRecursiveDeps(IEnumerable<JsonFile> jsonFiles)
    {
        var queue = jsonFiles.ToList();
        var queued = new HashSet<JsonFile>(queue);
        var varDeps = new HashSet<VarPackage>();
        var freeFileDeps = new HashSet<FreeFile>();

        while (queue.Count > 0)
        {
            var item = queue[^1];
            queue.RemoveAt(queue.Count - 1);

            var jsonFilesToScan = item.VarReferences.Where(t => !t.AlreadyCalculatedDeps)
                .SelectMany(t => t.JsonFiles)
                .Concat(item.FreeReferences.Where(t => !t.AlreadyCalculatedDeps && t.JsonFile != null).Select(t => t.JsonFile!));

            item.VarReferences.ForEach(t => varDeps.Add(t));
            item.FreeReferences.ForEach(t => freeFileDeps.Add(t));
            item.VarReferences.Where(t => t.AlreadyCalculatedDeps).SelectMany(t => t.AllResolvedVarDependencies).ForEach(t => varDeps.Add(t));
            item.FreeReferences.Where(t => t.AlreadyCalculatedDeps).SelectMany(t => t.AllResolvedFreeDependencies).ForEach(t => freeFileDeps.Add(t));

            foreach (var jsonFile in jsonFilesToScan)
            {
                if (queued.Contains(jsonFile))
                    continue;

                queued.Add(jsonFile);
                queue.Add(jsonFile);
            }
        }

        return (varDeps.ToList(), freeFileDeps.ToList());
    }
}