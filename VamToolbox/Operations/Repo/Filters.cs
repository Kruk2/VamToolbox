using System.Collections;
using VamToolbox.Helpers;
using VamToolbox.Models;

namespace VamToolbox.Operations.Repo;

public class ProfileModel : ICloneable
{
    public string Name { get; }
    public SortedSet<string> Dirs { get; }
    public SortedSet<string> Files { get; }

    public ProfileModel(SortedSet<string> files, SortedSet<string> dirs, string name)
    {
        Files = files;
        Dirs = dirs;
        Name = name;
    }

    public object Clone() => new ProfileModel(new SortedSet<string>(Files), new SortedSet<string>(Dirs), Name);
    public override string ToString() => Name;
}

public interface IVarFilters
{
    bool Matches(string path);
    void FromProfile(ProfileModel profile);
    (IEnumerable<VarPackage> vars, IEnumerable<FreeFile> freeFiles) GetFilesToMove(IList<VarPackage> vars);
}

public class VarFilters : IVarFilters
{
    private readonly List<string> _dirs = new();
    private readonly List<string> _files = new();

    private void AddFile(string file) => _files.Add(file);
    private void AddDir(string file) => _dirs.Add(file);

    public void FromProfile(ProfileModel profile)
    {
        _dirs.AddRange(profile.Dirs.Select(t => t.NormalizePathSeparators()));
        _files.AddRange(profile.Files.Select(t => t.NormalizePathSeparators()));
    }

    public (IEnumerable<VarPackage> vars, IEnumerable<FreeFile> freeFiles) GetFilesToMove(IList<VarPackage> vars)
    {
        var repoVars = vars.Where(t => !t.IsInVaMDir);
        var toCopy = repoVars.Where(t => Matches(t.FullPath)).ToList();

        var varDeps = toCopy
            .SelectMany(t => t.ResolvedVarDependencies)
            .Concat(toCopy)
            .DistinctBy(t => t.FullPath)
            .Where(t => !t.IsInVaMDir);

        var freeFilesDeps = toCopy
            .SelectMany(t => t.ResolvedFreeDependencies)
            .SelectMany(t => t.SelfAndChildren())
            .DistinctBy(t => t.FullPath)
            .Where(t => !t.IsInVaMDir);

        return (varDeps, freeFilesDeps);
    }

    public bool Matches(string path)
    {
        return _dirs.Any(path.StartsWith) || _files.Contains(path);
    }
}