using System.Dynamic;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Destructive;

public sealed class TrustAllVarsOperation : ITrustAllVarsOperation
{
    private readonly IProgressTracker _progressTracker;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private int _total;
    private int _progress;
    private int _trusted;
    private string _vamPrefsDir = null!;
    private OperationContext _context = null!;

    public TrustAllVarsOperation(IProgressTracker progressTracker, IFileSystem fs, ILogger logger)
    {
        _progressTracker = progressTracker;
        _fs = fs;
        _logger = logger;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars)
    {
        _context = context;
        _vamPrefsDir = Path.Combine(context.VamDir, "AddonPackagesUserPrefs");
        if (!context.DryRun)
            Directory.CreateDirectory(_vamPrefsDir);

        _progressTracker.InitProgress("Trusting all vars");
        _total = vars.Count;
        var depScanBlock = new ActionBlock<VarPackage>(TrustVar, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = context.Threads
        });

        foreach (var var in vars.Where(t => t.IsInVaMDir))
            depScanBlock.Post(var);

        depScanBlock.Complete();
        await depScanBlock.Completion;

        _progressTracker.Complete($"Trusted {_trusted} vars");
    }

    private void TrustVar(VarPackage var)
    {
        var prefFile = Path.Combine(_vamPrefsDir, Path.GetFileNameWithoutExtension(var.Name.Filename) + ".prefs");
        dynamic json = _fs.File.Exists(prefFile) ? JsonConvert.DeserializeObject<ExpandoObject>(_fs.File.ReadAllText(prefFile))! : new ExpandoObject();

        var hasPropertyDisabled = ((IDictionary<string, object>)json).ContainsKey("pluginsAlwaysDisabled");
        var hasPropertyEnabled = ((IDictionary<string, object>)json).ContainsKey("pluginsAlwaysEnabled");
        if (!hasPropertyDisabled || (hasPropertyDisabled && json.pluginsAlwaysDisabled != "true"))
        {
            if (!hasPropertyEnabled || (hasPropertyEnabled && json.pluginsAlwaysEnabled != "true"))
            {
                json.pluginsAlwaysEnabled = "true";
                json.pluginsAlwaysDisabled = "false";

                var serialized = JsonConvert.SerializeObject(json, Formatting.Indented);
                if (!_context.DryRun)
                    _fs.File.WriteAllText(prefFile, serialized);

                Interlocked.Increment(ref _trusted);
            }
        }

        _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _progress), _total, $"Trusting {var}"));
    }
}

public interface ITrustAllVarsOperation : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<VarPackage> vars);
}