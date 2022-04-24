using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLitePCL;
using VamRepacker.Helpers;
using VamRepacker.Logging;
using VamRepacker.Models;
using VamRepacker.Operations.Abstract;

namespace VamRepacker.Operations.Repo;

public sealed class CopyMissingVarDependenciesFromRepo :ICopyMissingVarDependenciesFromRepo
{
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;
    private readonly ISoftLinker _linker;
    private OperationContext _context = null!;

    public CopyMissingVarDependenciesFromRepo(IProgressTracker progressTracker, ILogger logger, ISoftLinker linker)
    {
        _reporter = progressTracker;
        _logger = logger;
        _linker = linker;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles,
        bool moveVars, bool shallow)
    {
        _reporter.InitProgress("Copying missing dependencies from REPO to VAM");
        _context = context;
        await _logger.Init("copy_missing_deps_from_repo.log");
        if (string.IsNullOrEmpty(_context.RepoDir))
        {
            _reporter.Complete("Missing repo dir. Aborting");
            return;
        }

        var (exitingVars, existingFiles) = await Task.Run(() => FindFilesToLink(vars, freeFiles, shallow));
        await Task.Run(() => LinkFiles(moveVars, existingFiles, exitingVars));
    }

    private void LinkFiles(
        bool moveVars, 
        IReadOnlyCollection<FreeFile> existingFiles, IReadOnlyCollection<VarPackage> exitingVars)
    {
        var count = existingFiles.Count + exitingVars.Count;
        var processed = 0;

        var varFolderDestination = Path.Combine(_context.VamDir, "AddonPackages", "other");
        if (!_context.DryRun)
            Directory.CreateDirectory(varFolderDestination);

        foreach (var existingVar in exitingVars.OrderBy(t => t.Name.Filename))
        {
            var varDestination = Path.Combine(varFolderDestination, Path.GetFileName(existingVar.FullPath));
            if (File.Exists(varDestination))
            {
                _logger.Log($"Skipping {varDestination} source: {existingVar.FullPath}. Already exists.");
                _reporter.Report(new ProgressInfo(++processed, count, existingVar.Name.Filename));
                continue;
            }
            if (moveVars && !_context.DryRun)
                File.Move(existingVar.FullPath, varDestination);
            else
            {
                var success = _linker.SoftLink(varDestination, existingVar.FullPath, _context.DryRun);
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
            var relativeToRoot = file.FullPath.RelativeTo(_context.RepoDir!);
            var destinationPath = Path.Combine(_context.VamDir, relativeToRoot);
            if (File.Exists(destinationPath))
            {
                _logger.Log($"SkippingDest: {destinationPath} source: {file.FullPath}. Already exists.");
                _reporter.Report(new ProgressInfo(++processed, count, file.FilenameWithoutExt));
                continue;
            }
            if (!_context.DryRun)
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (moveVars && !_context.DryRun)
            {
                File.Move(file.FullPath, destinationPath);
            }
            else
            {
                var success = _linker.SoftLink(destinationPath, file.FullPath, _context.DryRun);
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

#if DEBUG
        var v = vars.First(t => t.Name.Filename.Contains("Sapuzex.anime_dream"));
        var v2 = vars.First(t => t.Name.Filename.Contains("Sapuzex.ScenesCollection.2"));
        var jsonFile = v2.JsonFiles.First(t => t.File.FilenameLower.Contains("anime"));
        var because = vars
            .Where(t => t.IsInVaMDir && t.TrimmedResolvedVarDependencies.Contains(v));
        var jsonFiles = because.First().JsonFiles
            .Where(t => t.VarReferences.Contains(v)).ToList();
        var jsonReferences = jsonFiles.SelectMany(t => t.References.Where(t => t.IsVarReference && t.ParentVar == v))
            .ToList();
        #endif

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