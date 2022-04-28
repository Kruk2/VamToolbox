using System.Collections.Concurrent;

namespace VamToolbox.Logging;

public sealed class Logger : ILogger
{
    private ThreadSafeFileBuffer? _writer;

    public void Log(string message) => _writer?.Write(message);
    public async ValueTask Init(string filename)
    {
        if(_writer != null)
            await _writer.DisposeAsync();

        _writer = new ThreadSafeFileBuffer(Path.Combine(Environment.CurrentDirectory, filename));
    }

    public ValueTask DisposeAsync() => _writer?.DisposeAsync() ?? ValueTask.CompletedTask;
}

public sealed class ThreadSafeFileBuffer : IAsyncDisposable
{
    private const int FlushPeriodInMs = 100;
    private readonly StreamWriter _writer;
    private readonly ConcurrentQueue<string> _buffer = new();
    private readonly Timer _timer;
    private volatile bool _disposed, _requestStop;
    private readonly ManualResetEvent _stopped = new(false);

    public ThreadSafeFileBuffer(string filePath)
    {
        _writer = new StreamWriter(filePath, append: false);
        _timer = new Timer(TimerCallback, null, FlushPeriodInMs, Timeout.Infinite);
    }

    public void Write(string line)
    {
        _buffer.Enqueue(line);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _requestStop = true;
        _stopped.WaitOne();
        await _timer.DisposeAsync();

        FlushBuffer();
        await _writer.DisposeAsync();
        _disposed = true;
    }

    private void TimerCallback(object? _ = null)
    {
        if (_requestStop)
        {
            _stopped.Set();
            return;
        }

        FlushBuffer();

        _timer.Change(FlushPeriodInMs, Timeout.Infinite);
    }

    private void FlushBuffer()
    {
        while (_buffer.TryDequeue(out var current))
        {
            _writer.WriteLine(current);
        }
    }
}