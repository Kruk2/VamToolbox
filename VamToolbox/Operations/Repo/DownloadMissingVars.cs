using Newtonsoft.Json;
using RestEase;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Repo;

public sealed class DownloadMissingVars : IDownloadMissingVars
{
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;

    public DownloadMissingVars(IProgressTracker progressTracker, ILogger logger)
    {
        _reporter = progressTracker;
        _logger = logger;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles)
    {
        _reporter.InitProgress("Downloading missing vars from vam hub");
        await _logger.Init("download_missing_from_vam.log");
        int processed = 0;
        var unresolvedVars = await Task.Run(() => FindMissingReferences(vars, freeFiles));

        var vamResult = await QueryVam(unresolvedVars, vars.Select(t => t.Name).ToHashSet());
        if (vamResult.Count == 0)
        {
            _reporter.Complete("Downloaded 0 packages");
            return;
        }

        var folderDestination = Path.Combine(context.VamDir, "AddonPackages", "other");
        if (!context.DryRun)
            Directory.CreateDirectory(folderDestination);
        var count = vamResult.Count;

        using var handler = new HttpClientHandler { UseCookies = false };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36 Edg/99.0.1150.30");
            
        foreach (var (packageInfo, i) in vamResult.Zip(Enumerable.Range(0, count)))
        {
            var varDestination = Path.Combine(folderDestination, packageInfo.Filename);
            if(File.Exists(varDestination))
                continue;
            if (context.DryRun)
                continue;

            _logger.Log($"Downloading {packageInfo.Filename} {packageInfo.DownloadUrl}");
            _reporter.Report(new ProgressInfo(processed, count, $"Downloading {i}/{count} " + packageInfo.Filename));

            if (await DownloadVar(packageInfo, client, varDestination))
            {
                _logger.Log($"Downloaded {packageInfo.Filename} {packageInfo.DownloadUrl}");
            }

            _reporter.Report(new ProgressInfo(++processed, count, $"Downloaded {i}/{count} " + packageInfo.Filename));
        }

        _reporter.Complete($"Downloaded {processed} vars. Check download_missing_from_vam.log");
    }

    private async Task<bool> DownloadVar(PackageInfo packageInfo, HttpClient client, string destt)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, packageInfo.DownloadUrl);
        message.Headers.Add("Cookie", "vamhubconsent=yes");
        var response = await client.SendAsync(message);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Log($"Unable to download {packageInfo.DownloadUrl}. Status code: {response.StatusCode}");
            return false;
        }

        if (!response.Content.Headers.ContentLength.HasValue || response.Content.Headers.ContentType is not
            {
                MediaType: "application/octet-stream"
            })
        {
            _logger.Log(
                $"Unable to download {packageInfo.DownloadUrl}. Invalid size: {response.Content.Headers.ContentLength ?? 0} or content-type: {response.Content.Headers.ContentType?.MediaType ?? string.Empty}");
            return false;
        }

        await using var fs = new FileStream(destt, FileMode.CreateNew);
        await response.Content.CopyToAsync(fs);
        return true;
    }

    private static List<string> FindMissingReferences(IList<VarPackage> vars, IList<FreeFile> freeFiles)
    {
        var jsonFiles = vars.Where(t => t.IsInVaMDir).SelectMany(t => t.JsonFiles)
            .Concat(freeFiles.Where(t => t.IsInVaMDir && t.JsonFile != null).Select(t => t.JsonFile!));

        var unresolvedVars = jsonFiles
            .SelectMany(t => t.Missing)
            .Select(t => t.EstimatedVarName?.Filename)
            .Where(t => t != null)
            .Select(t => t!)
            .Distinct()
            .ToList();
        return unresolvedVars;
    }

    private async Task<List<PackageInfo>> QueryVam(IReadOnlyCollection<string> unresolvedVars,
        HashSet<VarPackageName> existingVars)
    {
        var service = RestClient.For<IVamService>();
        var query = new VamQuery { Packages = string.Join(',', unresolvedVars) };
        var result = await service.FindPackages(query);
        var parsedVars = unresolvedVars.Select(t =>
        {
            if (!VarPackageName.TryGet(t + ".var", out var name))
            {
                _logger.Log($"Unable to parse package name for unresolved reference: {t}");
                return null;
            }
            return name;
        })
            .Where(t => t != null)
            .Select(t => t!)
            .ToLookup(t => t.PackageNameWithoutVersion, StringComparer.OrdinalIgnoreCase);
        var packagesToDownload = new List<PackageInfo>();

        foreach (var packageInfo in result.Packages.Values.Where(t => !string.IsNullOrEmpty(t.DownloadUrl) && t.DownloadUrl != "null"))
        {
            if (!VarPackageName.TryGet(packageInfo.Filename, out var packageName))
            {
                _logger.Log($"Unable to parse package name from VaM service: {packageInfo.Filename} url: {packageInfo.DownloadUrl}");
                continue;
            }

            var matchedVars = parsedVars[packageName.PackageNameWithoutVersion];
            if (!matchedVars.Any())
            {
                _logger.Log($"Unable to find matching package for {packageInfo.Filename}");
                continue;
            }

            var shouldBeDownloaded = !existingVars.Contains(packageName) && matchedVars.Any(t => t.Version == -1 ||
                t.Version == packageName.Version ||
                (t.MinVersion && t.Version < packageName.Version));

            if(shouldBeDownloaded)
                packagesToDownload.Add(packageInfo);
        }

        return packagesToDownload.DistinctBy(t => t.DownloadUrl).ToList();
    }
}

public class VamResult
{
    [JsonProperty("packages")]
    public Dictionary<string, PackageInfo> Packages { get; set; } = null!;
}

public class PackageInfo
{
    [JsonProperty("filename")]
    public string Filename { get; set; } = string.Empty;
    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;
}

public class VamQuery
{
    [JsonProperty("source")]
    public string Source { get; set; } = "VaM";
    [JsonProperty("action")]
    public string Action { get; set; } = "findPackages";
    [JsonProperty("packages")]
    public string Packages { get; set; } = string.Empty;
}


[BaseAddress("https://hub.virtamate.com")]
public interface IVamService
{
    [Post("citizenx/api.php")]
    Task<VamResult> FindPackages([Body] VamQuery query);
}

public interface IDownloadMissingVars : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles);
}