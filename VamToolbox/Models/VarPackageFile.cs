namespace VamToolbox.Models;

public sealed class VarPackageFile : FileReferenceBase
{
    public VarPackage ParentVar { get; internal set; } = null!;

    private readonly List<VarPackageFile> _children = new();
    public override IReadOnlyCollection<VarPackageFile> Children => _children.AsReadOnly();

    public VarPackageFile(string localPath, long size, bool isInVamDir, DateTime modifiedTimestamp)
        : base(localPath, size, isInVamDir, modifiedTimestamp)
    {
    }

    public override void AddChildren(FileReferenceBase children)
    {
        _children.Add((VarPackageFile) children);
        children.ParentFile = this;
    }

    public IEnumerable<VarPackageFile> SelfAndChildren() => Children.Append(this);

    public override string ToString()
    {
        return base.ToString() + $" Var: {ParentVar.Name.Filename}";
    }
}