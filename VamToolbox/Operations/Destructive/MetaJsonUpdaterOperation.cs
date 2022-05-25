using System.Dynamic;
using System.IO.Abstractions;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Ionic.Zip;
using Newtonsoft.Json;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Destructive;
public interface IMetaJsonUpdaterOperation : IOperation
{
    Task Execute(OperationContext context, bool removeDependencies = false, bool disableMorphPreload = false);
}

public class MetaJsonUpdaterOperation : IMetaJsonUpdaterOperation
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IProgressTracker _progressTracker;
    private readonly ISoftLinker _softLinker;
    private int _total, _progress, _changesCount;
    private OperationContext _context = null!;
    private bool _removeDependencies, _disableMorphPreload;

    private readonly JsonSerializer _serializer = new() {
        Formatting = Formatting.Indented
    };

    public MetaJsonUpdaterOperation(IFileSystem fileSystem, ILogger logger, IProgressTracker progressTracker, ISoftLinker softLinker)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _progressTracker = progressTracker;
        _softLinker = softLinker;
    }

    public async Task Execute(OperationContext context, bool removeDependencies, bool disableMorphPreload)
    {
        _removeDependencies = removeDependencies;
        _disableMorphPreload = disableMorphPreload;

        _context = context;
        await _logger.Init("clear_meta_json_dependencies.txt");
        _progressTracker.InitProgress("Clearing");
        await RunInParallel(GetAddonDirs(), UpdateMetaJson);

        _progressTracker.Complete($"Processed {_total} meta.json files. Fixed: {_changesCount} files");
    }

    private async Task UpdateMetaJson(string varPath)
    {
        var oldModifiedDate = _fileSystem.FileInfo.FromFileName(varPath).LastWriteTimeUtc;
        var oldCreatedDate = _fileSystem.FileInfo.FromFileName(varPath).CreationTimeUtc;
        var varTmpPath = varPath + ".tmp";

        try {
            {
                await using var stream = _fileSystem.File.OpenRead(varPath);
                using var zip = ZipFile.Read(stream);
                zip.CaseSensitiveRetrieval = true;

                var metaFile = zip["meta.json"];
                if (metaFile is null) {
                    _logger.Log($"Skipping because meta.json not found: {varPath}");
                    return;
                }

                dynamic json;
                {
                    using var memoryStream = new MemoryStream();
                    metaFile.Extract(memoryStream);
                    memoryStream.Position = 0;

                    using var streamReader = new StreamReader(memoryStream, Encoding.UTF8);
                    using var jsonReader = new JsonTextReader(streamReader);

                    json = _serializer.Deserialize<ExpandoObject>(jsonReader);
                }
                var dict = (IDictionary<string, object>)json;
                var changed = false;
                if (_removeDependencies) {

                    if (dict.ContainsKey("dependencies") && ((IDictionary<string, object>)dict["dependencies"]).Count > 0) {
                        var depsCount = ((IDictionary<string, object>)dict["dependencies"]).Count;
                        dict["dependencies"] = new object();
                        changed = true;
                        _logger.Log($"Removing {depsCount} dependencies from {varPath}");
                    }

                    if (dict.ContainsKey("hadReferenceIssues") && (string)dict["hadReferenceIssues"] == "true") {
                        dict.Remove("hadReferenceIssues");
                        changed = true;
                        _logger.Log($"Removing 'hadReferenceIssues' from {varPath}");
                    }

                    if (dict.ContainsKey("referenceIssues") && ((List<object>)dict["referenceIssues"]).Count > 0) {
                        dict.Remove("referenceIssues");
                        changed = true;
                        _logger.Log($"Removing 'referenceIssues' from {varPath}");
                    }
                }

                if (_disableMorphPreload) {
                    var customOptionsExists = dict.ContainsKey("customOptions");
                    if (customOptionsExists) {
                        var customOptions = (IDictionary<string, object>)dict["customOptions"];
                        if (customOptions.ContainsKey("preloadMorphs") && (string)customOptions["preloadMorphs"] == "true") {
                            customOptions["preloadMorphs"] = "false";
                            changed = true;

                            _logger.Log($"Disabling 'preloadMorphs' for {varPath}");
                        }
                    }
                }

                if (!_context.DryRun && changed) {
                    var oldMetaCreationTime = metaFile.CreationTime;
                    var oldMetaModifiedTime = metaFile.LastModified;
                    zip.RemoveEntry(metaFile);

                    {
                        using var memoryStream = new MemoryStream();
                        {
                            await using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
                            using var jsonWriter = new JsonTextWriter(streamWriter);

                            _serializer.Serialize(jsonWriter, json);
                            memoryStream.Position = 0;
                        }
                        zip.AddEntry("meta.json", memoryStream.ToArray());
                        if (oldMetaCreationTime != default) {
                            zip["meta.json"].CreationTime = oldMetaCreationTime;
                        }
                        if (oldMetaModifiedTime != default) {
                            zip["meta.json"].LastModified = oldMetaModifiedTime;
                        }
                    }

                    await using (var outputStream = _fileSystem.File.OpenWrite(varTmpPath)) {
                        zip.Save(outputStream);
                    }
                    Interlocked.Increment(ref _changesCount);
                }
            }

            if (_fileSystem.File.Exists(varTmpPath)) {
                _fileSystem.File.Move(varTmpPath, varPath, true);
            }
        } catch (Exception e) {
            _logger.Log($"Unable to process {varPath}. Error: {e.Message}");
        } finally {
            if (!_context.DryRun) {
                _fileSystem.File.SetLastWriteTimeUtc(varPath, oldModifiedDate);
                _fileSystem.File.SetLastWriteTimeUtc(varPath, oldCreatedDate);
            }
            Interlocked.Increment(ref _progress);
            _progressTracker.Report(new ProgressInfo(_progress, _total, $"Clearing meta.json dependencies: {_fileSystem.Path.GetFileName(varPath)}"));
        }
    }

    private async Task RunInParallel(IEnumerable<string> varDirs, Func<string, Task> act)
    {
        var depScanBlock = new ActionBlock<string>(async t => await act(t), new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = _context.Threads
        });

        var files = varDirs
            .SelectMany(t => _fileSystem.Directory.EnumerateFiles(t, "*.var", SearchOption.AllDirectories))
            .Where(t => !_softLinker.IsSoftLink(t))
            .ToList();
        _total = files.Count;
        foreach (var file in files) {
            depScanBlock.Post(file);
        }

        depScanBlock.Complete();
        await depScanBlock.Completion;
    }

    private IEnumerable<string> GetAddonDirs()
    {
        var addonDir = _fileSystem.Path.Combine(_context.VamDir, "AddonPackages");
        if (_fileSystem.Directory.Exists(addonDir)) {
            yield return addonDir;
        }
        if (_context.RepoDir is not null && _fileSystem.Directory.Exists(_context.RepoDir)) {
            yield return _context.RepoDir;
        }
    }
}
