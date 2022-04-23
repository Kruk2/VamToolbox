using System;
using System.Collections.Generic;
using System.Linq;
using VamRepacker.Helpers;

namespace VamRepacker.Models;

public sealed class FreeFile : FileReferenceBase, IVamObjectWithDependencies
{
    public string FullPath { get; }
    private readonly List<FreeFile> _children = new();
    public override IReadOnlyCollection<FreeFile> Children => _children.AsReadOnly();

    private List<VarPackage>? _trimmedResolvedVarDependencies, _allResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies, _allResolvedFreeDependencies;
    public List<VarPackage> TrimmedResolvedVarDependencies => CalculateShallowDeps().Var;
    public List<VarPackage> AllResolvedVarDependencies => CalculateDeps().Var;
    public List<FreeFile> TrimmedResolvedFreeDependencies => CalculateShallowDeps().Free;
    public List<FreeFile> AllResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _allResolvedVarDependencies is not null || _trimmedResolvedVarDependencies is not null;

    public IEnumerable<string> UnresolvedDependencies => JsonFiles
        .SelectMany(t => t.Missing.Select(x => x.Value + " from " + t))
        .Distinct();

    public bool Dirty { get; set; }
    public DateTime ModifiedTimestamp { get; }

    public FreeFile(string path, string localPath, long size, bool isInVamDir, DateTime modifiedTimestamp)
        : base(localPath, size, isInVamDir)
    {
        FullPath = path.NormalizePathSeparators();
        ModifiedTimestamp = modifiedTimestamp;
    }

    public IEnumerable<FreeFile> SelfAndChildren() => Children.Append(this);

    public override string ToString() => LocalPath;

    public override void AddChildren(FileReferenceBase children)
    {
        _children.Add((FreeFile) children);
        children.ParentFile = this;
    }

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_allResolvedFreeDependencies is not null && _allResolvedVarDependencies is not null)
            return (_allResolvedVarDependencies, _allResolvedFreeDependencies);
        return (_allResolvedVarDependencies, _allResolvedFreeDependencies) = DependencyCalculator.CalculateAllVarRecursiveDeps(JsonFiles);
    }

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateShallowDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(JsonFiles);
    }

    public void ClearDependencies()
    {
        _allResolvedFreeDependencies = null;
        _allResolvedVarDependencies = null;
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}