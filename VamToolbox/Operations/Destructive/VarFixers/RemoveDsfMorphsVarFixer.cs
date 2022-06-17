using Ionic.Zip;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.Operations.Destructive.VarFixers;

public class RemoveDsfMorphsVarFixer : IVarFixer
{
    private readonly ILogger _logger;
    public RemoveDsfMorphsVarFixer(ILogger logger) => _logger = logger;

    public bool Process(VarPackage var, ZipFile zip, Lazy<IDictionary<string, object>?> metaFileLazy)
    {
        var dsfMorphs = zip.Entries.Where(IsDsfMorph).ToArray();

        if (dsfMorphs.Length > 0) {
            _logger.Log($"Removing {dsfMorphs.Length} dsf morphs from {var.FullPath}");
            zip.RemoveEntries(dsfMorphs);
            return true;
        }

        return false;
    }

    private static bool IsDsfMorph(ZipEntry zipFile)
    {
        return !zipFile.IsDirectory && Path.GetExtension(zipFile.FileName).Equals(".dsf", StringComparison.OrdinalIgnoreCase);
    }
}