using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace VamRepacker.Logging
{
    public class Logger : ILogger
    {
        private ThreadSafeFileBuffer _writer;

        public void Log(string message) => _writer?.Write(message);
        public void Init(string filename)
        {
            _writer?.Dispose();
            _writer = new ThreadSafeFileBuffer(Path.Combine(Environment.CurrentDirectory, filename));
        }

        public void Dispose() => _writer?.Dispose();
    }

    public class ThreadSafeFileBuffer : IDisposable
    {
        private const int FlushPeriodInMs = 500;
        private readonly StreamWriter _writer;
        private readonly ConcurrentQueue<string> _buffer = new();
        private readonly Timer _timer;

        public ThreadSafeFileBuffer(string filePath)
        {
            _writer = new StreamWriter(filePath, append: false);
            var flushPeriod = TimeSpan.FromMilliseconds(FlushPeriodInMs);
            _timer = new Timer(FlushBuffer, null, flushPeriod, flushPeriod);
        }

        public void Write(string line)
        {
            _buffer.Enqueue(line);
        }

        public void Dispose()
        {
            _timer.Dispose();
            FlushBuffer();
            _writer.Dispose();
        }

        private void FlushBuffer(object unused = null)
        {
            while (_buffer.TryDequeue(out var current))
            {
                _writer.WriteLine(current);
            }

            _writer.Flush();
        }
    }
}