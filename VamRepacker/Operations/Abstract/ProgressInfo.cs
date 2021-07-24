namespace VamRepacker.Operations.Abstract
{
    public class ProgressInfo
    {
        public int Processed { get; }
        public int Total { get; }
        public string Current { get; }

        public ProgressInfo(int scenesProcessed, int totalScenes, string current)
        {
            Processed = scenesProcessed;
            Total = totalScenes;
            Current = current;
        }
    }
}
