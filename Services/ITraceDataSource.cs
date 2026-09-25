using Tracealyzer.Models;

namespace Tracealyzer.Services;

public interface ITraceDataSource : IDisposable
{
    string Name { get; }
    bool IsConnected { get; }

    event Action<TraceEvent>? EventReceived;
    event Action<string>? StatusChanged;
    event Action<Exception>? ErrorOccurred;

    Task StartAsync();
    Task StopAsync();
}
