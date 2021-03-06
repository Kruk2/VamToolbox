namespace VamToolbox.Operations.Abstract;

public sealed class ProgressInfo
{
    public int Processed { get; }
    public int Total { get; }
    public string Current { get; }
    public bool ForceShow { get; }

    public ProgressInfo(int processed, int total, string current, bool forceShow = false)
    {
        Processed = processed;
        Total = total;
        Current = current;
        ForceShow = forceShow;
    }

    public ProgressInfo(string current)
    {
        Processed = 0;
        Total = 0;
        Current = current;
    }

    public ProgressInfo(string current, bool forceShow) : this(current)
    {
        ForceShow = forceShow;
    }
}