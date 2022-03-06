namespace VamRepacker.Operations.Abstract
{
    public record OperationContext
    {
        public string VamDir { get; init; }
        public string RepoDir { get; init; }
        public bool DryRun { get; init; }
        public int Threads { get; init; }
        public bool ShallowDeps { get; set; }
    }
}