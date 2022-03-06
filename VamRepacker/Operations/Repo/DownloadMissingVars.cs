using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestEase;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.Repo
{
    public class DownloadMissingVars : IDownloadMissingVars
    {
        private readonly IProgressTracker _reporter;
        private readonly ILogger _logger;
        private readonly IFileLinker _linker;

        public DownloadMissingVars(IProgressTracker progressTracker, ILogger logger, IFileLinker linker)
        {
            _reporter = progressTracker;
            _logger = logger;
            _linker = linker;
        }

        public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles)
        {
            _reporter.InitProgress();
            _logger.Init("download_missing_from_vam.log");
            int processed = 0;
            var jsonFiles = vars.Where(t => t.IsInVaMDir).SelectMany(t => t.JsonFiles)
                .Concat(freeFiles.Where(t => t.IsInVaMDir).SelectMany(t => t.JsonFiles));

            var unresolvedVars = jsonFiles
                .SelectMany(t => t.Missing)
                .Select(t => t.EstimatedVarName)
                .Where(t => t != null)
                .Distinct()
                .ToList();

            var vamResult = await QueryVam(unresolvedVars, vars.Select(t => t.Name).ToHashSet());
            if (vamResult.Count == 0)
            {
                _reporter.Complete("Downloaded 0 packages");
                return;
            }

            var destAddonPackagesOtherFolder = Path.Combine(context.VamDir, "AddonPackages", "other");
            if (!context.DryRun)
                Directory.CreateDirectory(destAddonPackagesOtherFolder);
            var count = vamResult.Count;

            using var handler = new HttpClientHandler { UseCookies = false };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36 Edg/99.0.1150.30");
            
            foreach (var packageInfo in vamResult)
            {
                var destt = Path.Combine(destAddonPackagesOtherFolder, packageInfo.Filename);
                if(File.Exists(destt))
                    continue;
                if (context.DryRun)
                    continue;

                _logger.Log($"Downloading {packageInfo.DownloadUrl}");
                _reporter.Report(new ProgressInfo(processed, count, "Downloading " + packageInfo.Filename));

                using var message = new HttpRequestMessage(HttpMethod.Get, packageInfo.DownloadUrl);
                message.Headers.Add("Cookie", "vamhubconsent=yes");
                var response = await client.SendAsync(message);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Log($"Unable to download {packageInfo.DownloadUrl}. Status code: {response.StatusCode}");
                    continue;
                }

                if (!response.Content.Headers.ContentLength.HasValue || response.Content.Headers.ContentType is not
                    {
                        MediaType: "application/octet-stream"
                    })
                {
                    _logger.Log($"Unable to download {packageInfo.DownloadUrl}. Invalid size: {response.Content.Headers.ContentLength ?? 0} or content-type: {response.Content.Headers.ContentType?.MediaType ?? string.Empty}");
                    continue;
                }

                await using var fs = new FileStream(destt, FileMode.CreateNew);
                await response.Content.CopyToAsync(fs);

                _logger.Log($"Downloaded {packageInfo.DownloadUrl}");
                _reporter.Report(new ProgressInfo(++processed, count, "Downloaded " + packageInfo.Filename));
            }

            _reporter.Complete($"Downloaded {processed} vars. Check download_missing_from_vam.log");
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
            }).Where(t => t != null).ToLookup(t => t.PackageNameWithoutVersion, StringComparer.OrdinalIgnoreCase);
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
        public Dictionary<string, PackageInfo> Packages { get; set; }
    }

    public class PackageInfo
    {
        [JsonProperty("filename")]
        public string Filename { get; set; }
        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; }
    }

    public class VamQuery
    {
        [JsonProperty("source")]
        public string Source { get; set; } = "VaM";
        [JsonProperty("action")]
        public string Action { get; set; } = "findPackages";
        [JsonProperty("packages")]
        public string Packages { get; set; }
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
}