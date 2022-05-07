using System.IO.Abstractions;
using VamToolbox.Helpers;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public interface IPreviewGrouper
{
    public void GroupsPreviews<T>(List<T> files) where T : FileReferenceBase;
}

public sealed class PreviewGrouper : IPreviewGrouper
{
    private readonly IFileSystem _fs;
    public PreviewGrouper(IFileSystem fs) => _fs = fs;

    public void GroupsPreviews<T>(List<T> files) where T : FileReferenceBase
    {
        var previewExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var filesWithPreview = new[] { ".vam", ".var", ".vap", ".vaj" };
        var filesMovedAsChildren = new HashSet<T>();
        var possibleFilesWithPreview = files.Where(t => filesWithPreview.Contains(t.ExtLower));
        var possiblePreviews = files.Where(t => previewExtensions.Contains(t.ExtLower)).ToDictionary(t => t.LocalPath);

        foreach (var possibleFileWithPreview in possibleFilesWithPreview) {
            foreach (var ext in previewExtensions) {
                var previewFileName = _fs.Path.Combine(Path.GetDirectoryName(possibleFileWithPreview.LocalPath), _fs.Path.GetFileNameWithoutExtension(possibleFileWithPreview.LocalPath) + ext).NormalizePathSeparators();
                if (!possiblePreviews.TryGetValue(previewFileName, out var child)) continue;

                possibleFileWithPreview.AddChildren(child);
                filesMovedAsChildren.Add(child);
            }
        }

        files.RemoveAll(t => filesMovedAsChildren.Contains(t));
    }
}