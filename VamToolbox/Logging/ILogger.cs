namespace VamToolbox.Logging;

public interface ILogger : IAsyncDisposable
{
    void Log(string message);
    ValueTask Init(string filename);
}