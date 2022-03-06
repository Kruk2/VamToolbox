using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.Repo
{
    public class CopyMissingVarDependenciesFromRepo :ICopyMissingVarDependenciesFromRepo
    {
        private readonly IProgressTracker _reporter;
        private readonly ILogger _logger;
        private readonly IFileLinker _linker;

        public CopyMissingVarDependenciesFromRepo(IProgressTracker progressTracker, ILogger logger, IFileLinker linker)
        {
            _reporter = progressTracker;
            _logger = logger;
            _linker = linker;
        }

        public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles,
            bool moveVars, bool shallow)
        {
            _reporter.InitProgress();
            _logger.Init("copy_missing_deps_from_repo.log");

            var (exitingVars, existingFiles) = await Task.Run(() => FindFilesToLink(vars, freeFiles, shallow));
            await Task.Run(() => LinkFiles(context, moveVars, existingFiles, exitingVars));
        }

        private void LinkFiles(
            OperationContext context, 
            bool moveVars, 
            IReadOnlyCollection<FreeFile> existingFiles, IReadOnlyCollection<VarPackage> exitingVars)
        {
            var count = existingFiles.Count + exitingVars.Count;
            var processed = 0;

            var varFolderDestination = Path.Combine(context.VamDir, "AddonPackages", "other");
            if (!context.DryRun)
                Directory.CreateDirectory(varFolderDestination);

            foreach (var existingVar in exitingVars.OrderBy(t => t.Name.Filename))
            {
                var varDestination = Path.Combine(varFolderDestination, Path.GetFileName(existingVar.FullPath));
                if (File.Exists(varDestination))
                    continue;
                if (moveVars && !context.DryRun)
                    File.Move(existingVar.FullPath, varDestination);
                else
                {
                    var success = _linker.SoftLink(varDestination, existingVar.FullPath, context.DryRun);
                    if (success != 0)
                    {
                        _logger.Log($"Error soft-link. Code {success} Dest: {varDestination} source: {existingVar.FullPath}");
                        _reporter.Complete("Failed. Unable to create symlink. Probably missing admin privilege.");
                        continue;
                    }
                }

                _logger.Log($"{(moveVars ? "Moved" : "Sym-link")}: {Path.GetFileName(varDestination)}");
                _reporter.Report(new ProgressInfo(++processed, count, existingVar.Name.Filename));
            }

            foreach (var file in existingFiles.OrderBy(t => t.FilenameLower))
            {
                var relativeToRoot = file.FullPath.RelativeTo(context.RepoDir);
                var destinationPath = Path.Combine(context.VamDir, relativeToRoot);
                if (File.Exists(destinationPath))
                    continue;
                if (!context.DryRun)
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                if (moveVars && !context.DryRun)
                {
                    File.Move(file.FullPath, destinationPath);
                }
                else
                {
                    var success = _linker.SoftLink(destinationPath, file.FullPath, context.DryRun);
                    if (success != 0)
                    {
                        _logger.Log($"Error soft-link. Code {success} Dest: {destinationPath} source: {file.FullPath}");
                        _reporter.Complete(
                            $"Failed. Unable to create symlink. Probably missing admin privilege. Error code: {success}.");
                        continue;
                    }
                }


                _logger.Log($"{(moveVars ? "Moved" : "Sym-link")}: {Path.GetFileName(file.FilenameWithoutExt)}");
                _reporter.Report(new ProgressInfo(++processed, count, file.FilenameWithoutExt));
            }

            _reporter.Complete($"Copied {count} vars/files. Check copy_missing_deps_from_repo.log");
        }

        private static (List<VarPackage> exitingVars, List<FreeFile> existingFiles) FindFilesToLink(IList<VarPackage> vars, IList<FreeFile> freeFiles, bool shallow)
        {
            var exitingVars = vars
                .Where(t => t.IsInVaMDir)
                .SelectMany(t => shallow ? t.TrimmedResolvedVarDependencies : t.AllResolvedVarDependencies)
                .Concat(freeFiles.Where(t => t.IsInVaMDir)
                    .SelectMany(t => shallow ? t.TrimmedResolvedVarDependencies : t.AllResolvedVarDependencies))
                .Where(t => !t.IsInVaMDir)
                .Distinct()
                .ToList();

            var existingFiles = vars
                .Where(t => t.IsInVaMDir)
                .SelectMany(t => shallow ? t.TrimmedResolvedFreeDependencies : t.AllResolvedFreeDependencies)
                .Concat(freeFiles.Where(t => t.IsInVaMDir)
                    .SelectMany(t => shallow ? t.TrimmedResolvedFreeDependencies : t.AllResolvedFreeDependencies))
                .Where(t => !t.IsInVaMDir)
                .SelectMany(t => t.SelfAndChildren())
                .Distinct()
                .ToList();
            return (exitingVars, existingFiles);
        }
    }

    public interface ICopyMissingVarDependenciesFromRepo : IOperation
    {
        Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, bool moveVars,
            bool shallow);
    }
}
