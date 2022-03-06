namespace VamRepacker.Operations.Abstract
{
    public class ProgressInfo
    {
        public int Processed { get; }
        public int Total { get; }
        public string Current { get; }

        public ProgressInfo(int processed, int total, string current)
        {
            Processed = processed;
            Total = total;
            Current = current;
        }

        public ProgressInfo(string current)
        {
            Processed = 0;
            Total = 0;
            Current = current;
        }
    }
}
