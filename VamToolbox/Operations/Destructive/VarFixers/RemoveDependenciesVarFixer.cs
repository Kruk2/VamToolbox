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
        if (metaFile.TryGetValue("dependencies", out object? value) && ((IDictionary<string, object>)value).Count > 0) {
            var depsCount = ((IDictionary<string, object>)value).Count;
            metaFile["dependencies"] = new object();
            _logger.Log($"Removing {depsCount} dependencies from {var.FullPath}");
            changed = true;
        }

        if (metaFile.TryGetValue("hadReferenceIssues", out var value2) && (string)value2 == "true") {
            metaFile.Remove("hadReferenceIssues");
            _logger.Log($"Removing 'hadReferenceIssues' from {var.FullPath}");
            changed = true;
        }

        if (metaFile.TryGetValue("referenceIssues", out var value3) && ((List<object>)value3).Count > 0) {
            metaFile.Remove("referenceIssues");
            _logger.Log($"Removing 'referenceIssues' from {var.FullPath}");
            changed = true;
        }

        return changed;
    }
}