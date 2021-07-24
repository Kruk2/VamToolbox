using System;

namespace VamRepacker.Logging
{
    public interface ILogger : IDisposable
    {
        void Log(string message);
        void Init(string filename);
    }
}
