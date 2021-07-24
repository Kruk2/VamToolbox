using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.Repo
{
    public class CopySelectedVarsWithDependenciesFromRepo : ICopySelectedVarsWithDependenciesFromRepo
    {
        private readonly IProgressTracker _reporter;
        private readonly ILogger _logger;
        private readonly IFileLinker _linker;

        public CopySelectedVarsWithDependenciesFromRepo(IProgressTracker reporter, ILogger logger, IFileLinker linker)
        {
            _reporter = reporter;
            _logger = logger;
            _linker = linker;
        }

        public Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IVarFilters varFilter)
        {
            _reporter.InitProgress();
            _logger.Init("copy_vars_from_repo.log");

            return Task.Run(() =>
            {
                var varsToCopy = FindVarsToCopy(vars, varFilter);
                var missingDependencies = varsToCopy.SelectMany(t => t.UnresolvedDependencies).Distinct().ToList();
                var addonPackages = Path.Combine(context.VamDir, "AddonPackages");

                foreach (var var in varsToCopy)
                {
                    var destination = Path.Combine(addonPackages, Path.GetRelativePath(context.RepoDir, var.FullPath));

                    bool result = _linker.SoftLink(destination, var.FullPath, context.DryRun);
                    if (!result)
                    {
                        _reporter.Complete("Failed. Unable to create symlink. Probably missing admin privilege.");
                        return;
                    }

                    _logger.Log($"Sym-link: {destination}");
                }

                foreach (var error in missingDependencies.OrderBy(e => e))
                    _logger.Log($"Var soft-link missing dependency: " + error);

                _reporter.Complete(
                    $"Copied {varsToCopy.Count} vars. Unresolved dependencies: {missingDependencies.Count}. Check copy_vars_from_repo.log");
            });
        }

        private static List<VarPackage> FindVarsToCopy(IEnumerable<VarPackage> vars, IVarFilters varFilters)
        {
            var repoVars = vars.Where(t => !t.IsInVaMDir);
            var toCopy = repoVars.Where(t => varFilters.Matches(t.FullPath)).ToList();

            // optimize? if there is already free file with the same uuid we could skip this var
            var dependencies = toCopy.SelectMany(t => t.TrimmedResolvedVarDependencies);
            return toCopy
                .Concat(dependencies)
                .DistinctBy(t => t.FullPath)
                .Where(t => !t.IsInVaMDir)
                .ToList();
        }
    }

    public interface ICopySelectedVarsWithDependenciesFromRepo : IOperation
    {
        Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IVarFilters varFilters);
    }
}
