using VamRepacker.Operations.Abstract;

namespace VamRepacker.Logging
{
    public interface IProgressTracker
    {
        void InitProgress();
        void Report(ProgressInfo progress);
        void Complete(string endingMessage);
    }
}