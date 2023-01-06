using Ionic.Zip;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.Operations.Destructive.VarFixers;

public class RemoveDependenciesVarFixer : IVarFixer
{
    private readonly ILogger _logger;
    public RemoveDependenciesVarFixer(ILogger logger) => _logger = logger;

    public bool Process(VarPackage var, ZipFile zip, Lazy<IDictionary<string, object>?> metaFileLazy)
    {
        var metaFile = metaFileLazy.Value;
        if (metaFile is null) {
            _logger.Log($"Skipping {nameof(RemoveDependenciesVarFixer)} because meta.json not found: {var.FullPath}");
            return false;
        }

        var changed = false;
        if (metaFile.ContainsKey("dependencies") && ((IDictionary<string, object>)metaFile["dependencies"]).Count > 0) {
            var depsCount = ((IDictionary<string, object>)metaFile["dependencies"]).Count;
            metaFile["dependencies"] = new object();
            _logger.Log($"Removing {depsCount} dependencies from {var.FullPath}");
            changed = true;
        }

        if (metaFile.TryGetValue("hadReferenceIssues", out var value) && (string)value == "true") {
            metaFile.Remove("hadReferenceIssues");
            _logger.Log($"Removing 'hadReferenceIssues' from {var.FullPath}");
            changed = true;
        }

        if (metaFile.TryGetValue("referenceIssues", out var value2) && ((List<object>)value2).Count > 0) {
            metaFile.Remove("referenceIssues");
            _logger.Log($"Removing 'referenceIssues' from {var.FullPath}");
            changed = true;
        }

        return changed;
    }
}