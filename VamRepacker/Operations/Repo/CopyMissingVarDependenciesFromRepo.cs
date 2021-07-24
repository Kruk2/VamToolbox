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

        public void ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, bool moveVars)
        {
            _reporter.InitProgress();
            _logger.Init("copy_missing_deps_from_repo.log");
            int processed = 0;

            var exitingVars = vars
                .Where(t => t.IsInVaMDir)
                .SelectMany(t => t.AllResolvedVarDependencies)
                .Where(t => !t.IsInVaMDir)
                .Concat(freeFiles.SelectMany(t => t.AllResolvedVarDependencies).Where(t => !t.IsInVaMDir))
                .Distinct()
                .ToList();

            var dest2 = Path.Combine(context.VamDir, "AddonPackages", "other");
            if (!context.DryRun)
                Directory.CreateDirectory(dest2);

            foreach (var existingVar in exitingVars.OrderBy(t => t.Name.Filename))
            {
                var destt = Path.Combine(dest2, Path.GetFileName(existingVar.FullPath));
                bool success = true;
                if (moveVars && !context.DryRun)
                    File.Copy(existingVar.FullPath, destt);
                else
                    success = _linker.SoftLink(destt, existingVar.FullPath, context.DryRun);

                if (!success)
                {
                    _reporter.Complete("Failed. Unable to create symlink. Probably missing admin privilege.");
                    return;
                }

                _logger.Log($"{(moveVars ? "Moved" : "Sym-link")}: {Path.GetFileName(destt)}");
                _reporter.Report(new ProgressInfo(++processed, exitingVars.Count, existingVar.Name.Filename));
            }

            _reporter.Complete($"Copied {processed} vars. Check copy_missing_deps_from_repo.log");
        }
    }

    public interface ICopyMissingVarDependenciesFromRepo : IOperation
    {
        void ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, bool moveVars);
    }
}
