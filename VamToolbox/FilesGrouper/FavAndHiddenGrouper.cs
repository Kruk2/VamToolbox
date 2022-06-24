using VamToolbox.Helpers;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;
public class FavAndHiddenGrouper : IFavAndHiddenGrouper
{
    private List<FreeFile> _freeFiles = null!;
    private List<VarPackage> _vars = null!;

    public Task Group(List<FreeFile> freeFiles, List<VarPackage> vars)
    {
        _freeFiles = freeFiles;
        _vars = vars;
        return Task.Run(GroupInternal);
    }

    private void GroupInternal()
    {
        GroupFavMorphs();
    }

    private void GroupFavMorphs()
    {
        var favDirs = KnownNames.MorphDirs.Select(t => Path.Combine(t, "favorites").NormalizePathSeparators()).ToArray();
        var favMorphs = _freeFiles
            .Where(t => t.ExtLower == ".fav" && favDirs.Any(x => t.LocalPath.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            .ToLookup(t => t.FilenameWithoutExt, t => (basePath: Path.GetDirectoryName(Path.GetDirectoryName(t.LocalPath))!.NormalizePathSeparators(), file: t), StringComparer.OrdinalIgnoreCase);

        var files = _freeFiles.Where(t => (t.Type & AssetType.ValidMorph) != 0).Cast<FileReferenceBase>().
            Concat(_vars.SelectMany(t => t.Files).Where(t => (t.Type & AssetType.ValidMorph) != 0));

        foreach (var file in files) {
            var fav = favMorphs[file.MorphName!]
                .SingleOrDefault(t => file.LocalPath.StartsWith(t.basePath + '/', StringComparison.OrdinalIgnoreCase));
            if (fav.file is not null) {
                file.FavFilePath = fav.file.LocalPath;
            }
        }

        _freeFiles.RemoveAll(t => t.ExtLower == ".fav");
    }
}

public interface IFavAndHiddenGrouper
{
    Task Group(List<FreeFile> freeFiles, List<VarPackage> vars);
}
