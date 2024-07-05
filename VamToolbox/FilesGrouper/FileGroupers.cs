using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public class FileGroupers : IFileGroupers
{
    private readonly IScriptGrouper _scriptGrouper;
    private readonly IMorphGrouper _morphGrouper;
    private readonly IPresetGrouper _presetGrouper;
    private readonly IPreviewGrouper _previewGrouper;
    private readonly IProgressTracker _reporter;

    public FileGroupers(IScriptGrouper scriptGrouper, 
        IMorphGrouper morphGrouper, 
        IPresetGrouper presetGrouper, 
        IPreviewGrouper previewGrouper,
        IProgressTracker reporter)
    {
        _scriptGrouper = scriptGrouper;
        _morphGrouper = morphGrouper;
        _presetGrouper = presetGrouper;
        _previewGrouper = previewGrouper;
        _reporter = reporter;
    }

    public async Task Group<T>(List<T> files, Func<string, Stream> openFileStream) where T : FileReferenceBase
    {
        _reporter.Report("Grouping cslist files", forceShow: true);
        await _scriptGrouper.GroupCslistRefs(files, openFileStream);
        _reporter.Report("Grouping morhps vmi", forceShow: true);
        await _morphGrouper.GroupMorphsVmi(files, openFileStream: openFileStream);
        _reporter.Report("Grouping presets", forceShow: true);
        await _presetGrouper.GroupPresets(files, openFileStream);
        _reporter.Report("Grouping previews", forceShow: true);
        _previewGrouper.GroupsPreviews(files);
    }
}

public interface IFileGroupers
{
    Task Group<T>(List<T> files, Func<string, Stream> openFileStream) where T : FileReferenceBase;
}
