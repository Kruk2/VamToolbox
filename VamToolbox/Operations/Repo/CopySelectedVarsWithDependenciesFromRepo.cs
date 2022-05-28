using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Repo;

public sealed class CopySelectedVarsWithDependenciesFromRepo : ICopySelectedVarsWithDependenciesFromRepo
{
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;
    private readonly ISoftLinker _linker;

    public CopySelectedVarsWithDependenciesFromRepo(IProgressTracker reporter, ILogger logger, ISoftLinker linker)
    {
        _reporter = reporter;
        _logger = logger;
        _linker = linker;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IVarFilters varFilters)
    {
        _reporter.InitProgress("Applying profile");
        await _logger.Init("copy_vars_from_repo.log");
        if (string.IsNullOrEmpty(context.RepoDir)) {
            _reporter.Complete("Unable to complete. Missing repo dir");
            return;
        }

        await Task.Run(() => ApplyProfiles(context, vars, varFilters));
    }

    private void ApplyProfiles(OperationContext context, IList<VarPackage> vars, IVarFilters varFilters)
    {
        var filesToCopy = DependencyCalculator.GetFilesToMove(varFilters, vars);
        var missingDependencies = filesToCopy
            .freeFiles.SelectMany(t => t.UnresolvedDependencies)
            .Concat(filesToCopy.vars.SelectMany(t => t.UnresolvedDependencies))
            .Distinct();

        int copied = 0, unresolved = 0;
        var addonPackages = Path.Combine(context.VamDir, "AddonPackages");
        var addonPackagesInRepo = Path.Combine(context.RepoDir!, "AddonPackages");

        foreach (var varPackage in filesToCopy.vars) 
        {
            var relativeToRepo = Path.GetRelativePath(context.RepoDir!, varPackage.FullPath);
            if (relativeToRepo.StartsWith("AddonPackages", StringComparison.OrdinalIgnoreCase)) {
                relativeToRepo = Path.GetRelativePath(addonPackagesInRepo, varPackage.FullPath);
            }

            var destination = Path.Combine(addonPackages, relativeToRepo);
            var result = _linker.SoftLink(destination, varPackage.FullPath, context.DryRun);
            if (!result)
            {
                _reporter.Complete($"Failed. Unable to create symlink. Probably missing admin privilege. Error code: {result}");
                return;
            }

            _logger.Log($"Sym-link: {destination}");
            copied++;
        }

        foreach (var freeFile in filesToCopy.freeFiles) 
        {
            var destination = Path.Combine(context.VamDir, freeFile.LocalPath);

            var result = _linker.SoftLink(destination, freeFile.FullPath, context.DryRun);
            if (!result) {
                _reporter.Complete($"Failed. Unable to create symlink. Probably missing admin privilege. Error code: {result}");
                return;
            }

            _logger.Log($"Sym-link: {destination}");
            copied++;
        }

        foreach (var error in missingDependencies.OrderBy(e => e)) 
        {
            _logger.Log($"Var soft-link missing dependency: {error}");
            unresolved++;
        }

        _reporter.Complete(
            $"Copied {copied} files. Unresolved dependencies: {unresolved}. Check copy_vars_from_repo.log");
    }
}

public interface ICopySelectedVarsWithDependenciesFromRepo : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IVarFilters varFilters);
}