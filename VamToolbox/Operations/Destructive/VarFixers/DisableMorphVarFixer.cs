using Ionic.Zip;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.Operations.Destructive.VarFixers;

public class DisableMorphVarFixer : IVarFixer
{
    private readonly ILogger _logger;

    public DisableMorphVarFixer(ILogger logger) => _logger = logger;

    public bool Process(VarPackage var, ZipFile zip, Lazy<IDictionary<string, object>?> metaFileLazy)
    {
        if (var.IsMorphPack) {
            _logger.Log($"Skipping {nameof(DisableMorphVarFixer)} because it's a morph-pack {var.FullPath}");
            return false;
        }

        var metaFile = metaFileLazy.Value;
        if (metaFile is null) {
            _logger.Log($"Skipping {nameof(DisableMorphVarFixer)} because meta.json not found: {var.FullPath}");
            return false;
        }

        var customOptionsExists = metaFile.ContainsKey("customOptions");
        if (customOptionsExists) {
            var customOptions = (IDictionary<string, object>)metaFile["customOptions"];
            if (customOptions.ContainsKey("preloadMorphs") && (string)customOptions["preloadMorphs"] == "true") {
                customOptions["preloadMorphs"] = "false";
                _logger.Log($"Disabling 'preloadMorphs' for {var.FullPath}");
                return true;
            }
        }

        return false;
    }
}