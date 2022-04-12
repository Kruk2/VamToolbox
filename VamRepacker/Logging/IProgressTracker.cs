using VamRepacker.Operations.Abstract;

namespace VamRepacker.Logging
{
    public interface IProgressTracker
    {
        void InitProgress(string startingMessage);
        void Report(ProgressInfo progress);
        void Report(string message, bool forceShow = false);
        void Complete(string endingMessage);
    }
}