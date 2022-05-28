using VamToolbox.Models;
using VamToolbox.Operations.Repo;

namespace VamToolbox.Helpers;

public static class DependencyCalculator
{
    public static (List<VarPackage>, List<FreeFile>) CalculateTrimmedDeps(IReadOnlyList<JsonFile> jsonFiles)
    {
        var queue = jsonFiles.SelectMany(t => t.References).Distinct().ToList();
        var processedFiles = new HashSet<FileReferenceBase>(jsonFiles.Select(t => t.File));
        var varDeps = new HashSet<VarPackage>();
        var freeFileDeps = new HashSet<FreeFile>();

        while (queue.Count > 0) {
            var item = queue[^1];
            queue.RemoveAt(queue.Count - 1);

            var ext = item.ToFile.ExtLower;
            if (item.IsVarReference)
                varDeps.Add(item.ToParentVar);
            else
                freeFileDeps.Add(item.ToFreeFile);

            if (!KnownNames.ExtReferencesToPresets.Contains(ext))
                continue;

            if (processedFiles.Contains(item.ToFile))
                continue;

            if (item.ToFile.JsonFile is not null)
                queue.AddRange(item.ToFile.JsonFile.References);
            processedFiles.Add(item.ToFile);
        }

        return (varDeps.ToList(), freeFileDeps.ToList());
    }

    public static (IEnumerable<VarPackage> vars, IEnumerable<FreeFile> freeFiles) GetFilesToMove(IVarFilters filters, IList<VarPackage> vars)
    {
        var repoVars = vars.Where(t => !t.IsInVaMDir);
        var toCopy = repoVars.Where(t => filters.Matches(t.FullPath)).ToList();

        var varDeps = toCopy
            .SelectMany(t => t.ResolvedVarDependencies)
            .Concat(toCopy)
            .DistinctBy(t => t.FullPath)
            .Where(t => !t.IsInVaMDir);

        var freeFilesDeps = toCopy
            .SelectMany(t => t.ResolvedFreeDependencies)
            .SelfAndChildren()
            .DistinctBy(t => t.FullPath)
            .Where(t => !t.IsInVaMDir);

        return (varDeps, freeFilesDeps);
    }

    public static (IEnumerable<VarPackage> vars, IEnumerable<FreeFile> freeFiles) GetFilesToMove(IList<VarPackage> vars, IList<FreeFile> freeFiles)
    {
        var dependedVarsToMove = vars
            .Where(t => t.IsInVaMDir)
            .SelectMany(t => t.ResolvedVarDependencies)
            .Concat(freeFiles.Where(t => t.IsInVaMDir).SelectMany(t => t.ResolvedVarDependencies))
            .Where(t => !t.IsInVaMDir)
            .Distinct()
            .ToList();

        var dependedFreeFilesToMove = vars
            .Where(t => t.IsInVaMDir)
            .SelectMany(t => t.ResolvedFreeDependencies)
            .Concat(freeFiles.Where(t => t.IsInVaMDir).SelectMany(t => t.ResolvedFreeDependencies))
            .Where(t => !t.IsInVaMDir)
            .SelfAndChildren()
            .ToList();

        return (dependedVarsToMove, dependedFreeFilesToMove);
    }
}