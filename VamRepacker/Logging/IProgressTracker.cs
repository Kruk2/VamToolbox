using VamRepacker.Operations.Abstract;

namespace VamRepacker.Logging
{
    public interface IProgressTracker
    {
        void InitProgress(string startingMessage);
        void Report(ProgressInfo progress);
        void Report(string message);
        void Complete(string endingMessage);
    }
}