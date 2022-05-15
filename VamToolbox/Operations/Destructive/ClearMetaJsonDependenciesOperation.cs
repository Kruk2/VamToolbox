using System.Dynamic;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Destructive;
public interface IClearMetaJsonDependenciesOperation : IOperation
{
    Task Execute(OperationContext context);
}

public class ClearMetaJsonDependenciesOperation : IClearMetaJsonDependenciesOperation
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IProgressTracker _progressTracker;
    private int _total, _progress;
    private OperationContext _context = null!;

    private readonly JsonSerializer _serializer = new() {
        Formatting = Formatting.Indented
    };

    public ClearMetaJsonDependenciesOperation(IFileSystem fileSystem, ILogger logger, IProgressTracker progressTracker)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _progressTracker = progressTracker;
    }

    public async Task Execute(OperationContext context)
    {
        _context = context;
        await _logger.Init("clear_meta_json_dependencies.txt");
        _progressTracker.InitProgress("Clearing");
        await RunInParallel(GetAddonDirs(), ClearMetaJson);

        _progressTracker.Complete($"Backed up {_total} meta.json");
    }

    private async Task ClearMetaJson(string varPath)
    {
        try {
            await using var stream = _context.DryRun ? _fileSystem.File.OpenRead(varPath) : _fileSystem.File.Open(varPath, FileMode.Open, FileAccess.ReadWrite);
            using var zip = new ZipArchive(stream, _context.DryRun ? ZipArchiveMode.Read : ZipArchiveMode.Update, true);

            var metaFile = zip.GetEntry("meta.json");
            if (metaFile is null) {
                Interlocked.Increment(ref _progress);
                return;
            }

            dynamic json;
            {
                using var streamReader = new StreamReader(metaFile.Open(), Encoding.UTF8);
                using var jsonReader = new JsonTextReader(streamReader);

                json = _serializer.Deserialize<ExpandoObject>(jsonReader);
            }

            var dict = ((IDictionary<string, object>)json);
            dict["dependencies"] = new object();
            dict.Remove("hadReferenceIssues");
            dict.Remove("referenceIssues");

            if (!_context.DryRun) {
                {
                    metaFile.Delete();
                    metaFile = zip.CreateEntry("meta.json");
                    await using var streamWriter = new StreamWriter(metaFile.Open(), Encoding.UTF8);
                    using var jsonWriter = new JsonTextWriter(streamWriter);

                    _serializer.Serialize(jsonWriter, json);
                }
            }

        } catch (Exception e) {
            _logger.Log($"Unable to process {varPath}. Error: {e.Message}");
        }

        Interlocked.Increment(ref _progress);
        _progressTracker.Report($"Clearing meta.json dependencies: {_fileSystem.Path.GetFileName(varPath)}");
        _logger.Log($"Clearing meta.json dependencies: {varPath}");
    }

    private async Task RunInParallel(IEnumerable<string> varDirs, Func<string, Task> act)
    {
        var depScanBlock = new ActionBlock<string>(async t => await act(t), new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = _context.Threads
        });

        var files = varDirs.SelectMany(t => _fileSystem.Directory.EnumerateFiles(t, "*.var")).ToList();
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
