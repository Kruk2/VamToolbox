namespace VamToolbox.Operations.Abstract;

public sealed record OperationContext
{
    public string VamDir { get; init; } = string.Empty;
    public string? RepoDir { get; init; }
    public bool DryRun { get; init; }
    public int Threads { get; init; }
}