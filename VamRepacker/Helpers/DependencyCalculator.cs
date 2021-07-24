using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreLinq;
using VamRepacker.Models;

namespace VamRepacker.Helpers
{
    public static class DependencyCalculator
    {
        public static (List<VarPackage>, List<FreeFile>) CalculateVarRecursiveDeps(IEnumerable<JsonFile> jsonFiles)
        {
            var queue = jsonFiles.SelectMany(t => t.References).Distinct().ToList();
            var queued = new HashSet<JsonReference>(queue);
            var varDeps = new HashSet<VarPackage>();
            var freeFileDeps = new HashSet<FreeFile>();

            while (queue.Count > 0)
            {
                var item = queue[^1];
                queue.RemoveAt(queue.Count - 1);

                var ext = item.IsVarReference ? item.VarFile.ExtLower : item.File.ExtLower;
                if (!KnownNames.ExtReferencesToPresets.Contains(ext))
                    continue;

                IEnumerable<JsonReference> references = item.IsVarReference ? item.VarFile.JsonReferences : item.File.JsonReferences;

                if (item.IsVarReference)
                    varDeps.Add(item.VarFile.ParentVar);
                else
                    freeFileDeps.Add(item.File);

                foreach (var reference in references)
                {
                    if (queued.Contains(reference))
                        continue;

                    queued.Add(reference);
                    queue.Add(reference);
                }
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

                var jsonFilesToScan = item.VarReferences
                    .SelectMany(t => t.JsonFiles)
                    .Concat(item.FreeReferences.SelectMany(t => t.JsonFiles));

                item.VarReferences.ForEach(t => varDeps.Add(t));
                item.FreeReferences.ForEach(t => freeFileDeps.Add(t));

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
}
