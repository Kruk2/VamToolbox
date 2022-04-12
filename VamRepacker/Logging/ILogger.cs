using System;
using System.Threading.Tasks;

namespace VamRepacker.Logging;

public interface ILogger : IAsyncDisposable
{
    void Log(string message);
    ValueTask Init(string filename);
}