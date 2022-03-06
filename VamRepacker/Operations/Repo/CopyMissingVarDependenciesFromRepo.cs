using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public void ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles,
            bool moveVars, bool shallow)
        {
            _reporter.InitProgress();
            _logger.Init("copy_missing_deps_from_repo.log");
            int processed = 0;

            var exitingVars = vars
                .Where(t => t.IsInVaMDir)
                .SelectMany(t => shallow ? t.TrimmedResolvedVarDependencies : t.AllResolvedVarDependencies)
                .Concat(freeFiles.Where(t => t.IsInVaMDir).SelectMany(t => shallow ? t.TrimmedResolvedVarDependencies : t.AllResolvedVarDependencies))
                .Where(t => !t.IsInVaMDir)
                .Distinct()
                .ToList();

            var existingFiles = vars
                .Where(t => t.IsInVaMDir)
                .SelectMany(t => shallow ? t.TrimmedResolvedFreeDependencies : t.AllResolvedFreeDependencies)
                .Concat(freeFiles.Where(t => t.IsInVaMDir).SelectMany(t => shallow ? t.TrimmedResolvedFreeDependencies : t.AllResolvedFreeDependencies))
                .Where(t => !t.IsInVaMDir)
                .SelectMany(t => t.SelfAndChildren())
                .Distinct()
                .ToList();

            var destAddonPackagesOtherFolder = Path.Combine(context.VamDir, "AddonPackages", "other");
            if (!context.DryRun)
                Directory.CreateDirectory(destAddonPackagesOtherFolder);
            var count = existingFiles.Count + exitingVars.Count;

            foreach (var existingVar in exitingVars.OrderBy(t => t.Name.Filename))
            {
                var destt = Path.Combine(destAddonPackagesOtherFolder, Path.GetFileName(existingVar.FullPath));
                if (File.Exists(destt))
                    continue;
                if (moveVars && !context.DryRun)
                    File.Move(existingVar.FullPath, destt);
                else
                {
                    var success = _linker.SoftLink(destt, existingVar.FullPath, context.DryRun);
                    if (success != 0)
                    {
                        _logger.Log($"Error soft-link. Code {success} Dest: {destt} source: {existingVar.FullPath}");
                        _reporter.Complete("Failed. Unable to create symlink. Probably missing admin privilege.");
                        return;
                    }
                }

                _logger.Log($"{(moveVars ? "Moved" : "Sym-link")}: {Path.GetFileName(destt)}");
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
                        _reporter.Complete($"Failed. Unable to create symlink. Probably missing admin privilege. Error code: {success}.");
                        return;
                    }
                }
                

                _logger.Log($"{(moveVars ? "Moved" : "Sym-link")}: {Path.GetFileName(file.FilenameWithoutExt)}");
                _reporter.Report(new ProgressInfo(++processed, count, file.FilenameWithoutExt));
            }

            _reporter.Complete($"Copied {processed} vars/files. Check copy_missing_deps_from_repo.log");
        }
    }

    public interface ICopyMissingVarDependenciesFromRepo : IOperation
    {
        void ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, bool moveVars,
            bool shallow);
    }
}
