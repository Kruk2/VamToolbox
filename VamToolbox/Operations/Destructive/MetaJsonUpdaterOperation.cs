using Ionic.Zip;
using Newtonsoft.Json;
using System.Dynamic;
using System.IO.Abstractions;
using System.Text;
using System.Threading.Tasks.Dataflow;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Destructive;
public interface IMetaJsonUpdaterOperation : IOperation
{
    Task Execute(OperationContext context, IEnumerable<VarPackage> vars, bool removeDependencies = false, bool disableMorphPreload = false);
}

public class MetaJsonUpdaterOperation : IMetaJsonUpdaterOperation
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IProgressTracker _progressTracker;
    private int _total, _progress, _changesCount, _errors;
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
    }

    public async Task Execute(OperationContext context, IEnumerable<VarPackage> vars, bool removeDependencies, bool disableMorphPreload)
    {
        _removeDependencies = removeDependencies;
        _disableMorphPreload = disableMorphPreload;

        _context = context;
        await _logger.Init("clear_meta_json_dependencies.txt");
        _progressTracker.InitProgress("Clearing");
        await RunInParallel(vars, UpdateMetaJson);

        _progressTracker.Complete($"Processed {_total} meta.json files. Fixed: {_changesCount} files. Errors {_errors}");
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

                var json = ReadMetaJson(metaFile);
                var dict = (IDictionary<string, object>)json;
                var changed = false;
                if (_removeDependencies) {
                    changed |= RemoveDependecies(varPath, dict);
                }

                if (_disableMorphPreload) {
                    changed |= DisableMorphPreload(varPath, dict);
                }

                if (changed) {

                    if (!_context.DryRun) {
                        BackupMeta(zip, metaFile, varPath);
                        await WriteNewMetaFile(json, zip, metaFile);
                        await using var outputStream = _fileSystem.File.OpenWrite(varTmpPath);
                        zip.Save(outputStream);
                    }

                    Interlocked.Increment(ref _changesCount);
                }
            }

            if (_fileSystem.File.Exists(varTmpPath)) {
                _fileSystem.File.Move(varTmpPath, varPath, true);
            }
        } catch (Exception e) {
            Interlocked.Increment(ref _errors);
            _logger.Log($"Unable to process {varPath}. Error: {e.Message}");
        } finally {
            if (!_context.DryRun) {
                _fileSystem.File.SetLastWriteTimeUtc(varPath, oldModifiedDate);
                _fileSystem.File.SetCreationTimeUtc(varPath, oldCreatedDate);
            }
            Interlocked.Increment(ref _progress);
            _progressTracker.Report(new ProgressInfo(_progress, _total, $"Processing meta.json: {_fileSystem.Path.GetFileName(varPath)}"));
        }
    }

    private async Task WriteNewMetaFile(dynamic json, ZipFile zip, ZipEntry metaFile)
    {
        var oldMetaCreationTime = metaFile.CreationTime;
        var oldMetaModifiedTime = metaFile.LastModified;
        zip.RemoveEntry(metaFile);
        using var memoryStream = new MemoryStream();
        {
            await using var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
            using var jsonWriter = new JsonTextWriter(streamWriter);

            _serializer.Serialize(jsonWriter, json);
        }

        memoryStream.Position = 0;
        zip.AddEntry("meta.json", memoryStream.ToArray());
        RestoreTimes(oldMetaCreationTime, zip, oldMetaModifiedTime);
    }

    private static void RestoreTimes(DateTime oldMetaCreationTime, ZipFile zip, DateTime oldMetaModifiedTime)
    {
        if (oldMetaCreationTime != default) {
            zip["meta.json"].CreationTime = oldMetaCreationTime;
        }

        if (oldMetaModifiedTime != default) {
            zip["meta.json"].LastModified = oldMetaModifiedTime;
        }
    }

    private dynamic ReadMetaJson(ZipEntry metaFile)
    {
        using var memoryStream = new MemoryStream();
        metaFile.Extract(memoryStream);
        memoryStream.Position = 0;

        using var streamReader = new StreamReader(memoryStream, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);

        dynamic json = _serializer.Deserialize<ExpandoObject>(jsonReader);
        return json;
    }

    private bool DisableMorphPreload(string varPath, IDictionary<string, object> dict)
    {
        var customOptionsExists = dict.ContainsKey("customOptions");
        if (customOptionsExists)
        {
            var customOptions = (IDictionary<string, object>)dict["customOptions"];
            if (customOptions.ContainsKey("preloadMorphs") && (string)customOptions["preloadMorphs"] == "true")
            {
                customOptions["preloadMorphs"] = "false";
                _logger.Log($"Disabling 'preloadMorphs' for {varPath}");
                return true;
            }
        }

        return false;
    }

    private bool RemoveDependecies(string varPath, IDictionary<string, object> dict)
    {
        var changed = false;
        if (dict.ContainsKey("dependencies") && ((IDictionary<string, object>)dict["dependencies"]).Count > 0)
        {
            var depsCount = ((IDictionary<string, object>)dict["dependencies"]).Count;
            dict["dependencies"] = new object();
            _logger.Log($"Removing {depsCount} dependencies from {varPath}");
            changed = true;
        }

        if (dict.ContainsKey("hadReferenceIssues") && (string)dict["hadReferenceIssues"] == "true")
        {
            dict.Remove("hadReferenceIssues");
            _logger.Log($"Removing 'hadReferenceIssues' from {varPath}");
            changed = true;
        }

        if (dict.ContainsKey("referenceIssues") && ((List<object>)dict["referenceIssues"]).Count > 0)
        {
            dict.Remove("referenceIssues");
            _logger.Log($"Removing 'referenceIssues' from {varPath}");
            changed = true;
        }

        return changed;
    }

    private async Task RunInParallel(IEnumerable<VarPackage> vars, Func<string, Task> act)
    {
        var depScanBlock = new ActionBlock<string>(async t => await act(t), new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = _context.Threads
        });

        var files = vars
            .Where(t => t.SourcePathIfSoftLink is null)
            .Where(t => _removeDependencies || (_disableMorphPreload && !t.IsMorphPack))
            .Select(t => t.FullPath)
            .ToList();

        _total = files.Count;
        foreach (var file in files) {
            depScanBlock.Post(file);
        }

        depScanBlock.Complete();
        await depScanBlock.Completion;
    }

    private void BackupMeta(ZipFile zip, ZipEntry metaFile, string varPath)
    {
        try {
            var backupFile = zip["meta.json" + KnownNames.BackupExtension];
            if (backupFile is not null) {
                _logger.Log($"Ignoring backups because it already exists {varPath}");
                return;
            }

            using (var reader = new MemoryStream()) {
                metaFile.Extract(reader);
                reader.Position = 0;
                zip.AddEntry("meta.json" + KnownNames.BackupExtension, reader.ToArray());
            }

            _logger.Log($"Backing up meta.json in {varPath}");

        } catch (Exception e) {
            _logger.Log($"Unable to backup meta.json in {varPath}. Error: {e.Message}");
        }
    }
}
