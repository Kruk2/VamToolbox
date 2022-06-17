using Ionic.Zip;
using Newtonsoft.Json;
using System.Dynamic;
using System.IO.Abstractions;
using System.Text;
using System.Threading.Tasks.Dataflow;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive.VarFixers;

namespace VamToolbox.Operations.Destructive;

public interface IVarFixerOperation : IOperation
{
    Task Execute(OperationContext context, IEnumerable<VarPackage> vars, IEnumerable<IVarFixer> varFixers);
}

public class VarFixerOperation : IVarFixerOperation
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IProgressTracker _progressTracker;
    private int _total, _progress, _changesCount, _errors;
    private OperationContext _context = null!;
    private IEnumerable<IVarFixer> _varFixers = null!;

    private readonly JsonSerializer _serializer = new() {
        Formatting = Formatting.Indented
    };

    public VarFixerOperation(IFileSystem fileSystem, ILogger logger, IProgressTracker progressTracker)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _progressTracker = progressTracker;
    }

    public async Task Execute(OperationContext context, IEnumerable<VarPackage> vars, IEnumerable<IVarFixer> varFixers)
    {
        _varFixers = varFixers;

        _context = context;
        await _logger.Init("var_fixer.txt");
        _progressTracker.InitProgress("Clearing");
        await RunInParallel(vars, ProcessVar);

        _progressTracker.Complete($"Processed {_total} var files. Fixed: {_changesCount} files. Errors {_errors}");
    }

    private async Task ProcessVar(VarPackage var)
    {
        var varPath = var.FullPath;
        var oldModifiedDate = _fileSystem.FileInfo.FromFileName(varPath).LastWriteTimeUtc;
        var oldCreatedDate = _fileSystem.FileInfo.FromFileName(varPath).CreationTimeUtc;
        var varTmpPath = varPath + ".tmp";

        try {
            {
                await using var stream = _fileSystem.File.OpenRead(varPath);
                using var zip = ZipFile.Read(stream);
                zip.CaseSensitiveRetrieval = true;

                var metaFile = zip["meta.json"];
                var metaContentLazy = new Lazy<IDictionary<string, object>?>(() => ReadMetaJson(metaFile));
                var changed = RunFixers(var, zip, metaContentLazy);

                if (changed) {

                    if (!_context.DryRun) {
                        if (metaContentLazy.Value is not null) {
                            BackupMeta(zip, metaFile, varPath);
                            await WriteNewMetaFile(metaContentLazy.Value, zip, metaFile);
                        }

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

    private bool RunFixers(VarPackage var, ZipFile zip, Lazy<IDictionary<string, object>?> metaContentLazy)
    {
        var changed = false;
        foreach (var varFixer in _varFixers) {
            changed |= varFixer.Process(var, zip, metaContentLazy);
        }

        return changed;
    }

    private async Task WriteNewMetaFile(dynamic? json, ZipFile zip, ZipEntry metaFile)
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

    private IDictionary<string, object>? ReadMetaJson(ZipEntry? metaFile)
    {
        if (metaFile is null) {
            return null;
        }

        using var memoryStream = new MemoryStream();
        metaFile.Extract(memoryStream);
        memoryStream.Position = 0;

        using var streamReader = new StreamReader(memoryStream, Encoding.UTF8);
        using var jsonReader = new JsonTextReader(streamReader);

        return _serializer.Deserialize<ExpandoObject>(jsonReader)!;
    }

    private async Task RunInParallel(IEnumerable<VarPackage> vars, Func<VarPackage, Task> act)
    {
        var depScanBlock = new ActionBlock<VarPackage>(async t => await act(t), new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = _context.Threads
        });

        var files = vars
            .Where(t => t.SourcePathIfSoftLink is null)
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
