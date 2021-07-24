using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.NotDestructive
{
    public class ScanFilesOperation : IScanFilesOperation
    {
        private readonly IProgressTracker _reporter;
        private readonly IFileSystem _fs;
        private readonly ILogger _logger;
        private readonly ILifetimeScope _scope;
        private OperationContext _context;

        public ScanFilesOperation(IProgressTracker reporter, IFileSystem fs, ILogger logger, ILifetimeScope scope)
        {
            _reporter = reporter;
            _fs = fs;
            _logger = logger;
            _scope = scope;
        }

        public async Task<List<FreeFile>> ExecuteAsync(OperationContext context)
        {
            var files = new List<FreeFile>();
            _reporter.InitProgress();
            _logger.Init("scan_files.log");
            _context = context;


            await Task.Run(async () =>
            {
                files.AddRange(ScanFolder("Custom"));
                files.AddRange(ScanFolder("Saves"));

                var favDirs = KnownNames.MorphDirs.Select(t => Path.Combine(t, "favorites").NormalizePathSeparators()).ToArray();
                var favMorphs = files
                    .Where(t => t.ExtLower == ".fav" && favDirs.Any(x => t.LocalPath.StartsWith(x)))
                    .ToLookup(t => t.FilenameWithoutExt, t => (basePath: KnownNames.MorphDirs.Single(x => t.LocalPath.StartsWith(x)), file: (FileReferenceBase)t));

                Stream OpenFileStream(string p) => _fs.File.OpenRead(_fs.Path.Combine(_context.VamDir, p));

                await _scope.Resolve<IScriptGrouper>().GroupCslistRefs(files, OpenFileStream);
                await _scope.Resolve<IMorphGrouper>().GroupMorphsVmi(files, varName: null, openFileStream: OpenFileStream, favMorphs);
                await _scope.Resolve<IPresetGrouper>().GroupPresets(files, varName: null, OpenFileStream);
                _scope.Resolve<IPreviewGrouper>().GroupsPreviews(files);
            });

            _reporter.Complete($"Scanned {files.Count} files in the Saves and Custom folders. Check scan_files.log");

            return files;
        }

        private IEnumerable<FreeFile> ScanFolder(string folder)
        {
            var vam = _context.VamDir;
            var files = _fs.Directory
                .EnumerateFiles(_fs.Path.Combine(vam, folder), "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(@"\."))
                .Select(f => new FreeFile(f, f.RelativeTo(vam), _fs.FileInfo.FromFileName(f).Length))
                .ToList();

            return files;
        }
    }

    public interface IScanFilesOperation : IOperation
    {
        Task<List<FreeFile>> ExecuteAsync(OperationContext context);
    }
}
