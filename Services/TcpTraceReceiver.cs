using System.IO;
using System.Net.Sockets;
using System.Text;
using Tracealyzer.Models;

namespace Tracealyzer.Services;

public class TcpTraceReceiver : ITraceDataSource
{
    private TcpClient? _tcpClient;
    private CancellationTokenSource? _cts;
    private bool _isConnected;

    public string Name => $"TCP ({Host}:{Port})";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 50000;
    public bool IsConnected => _isConnected;

    public event Action<TraceEvent>? EventReceived;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? ErrorOccurred;

    public async Task StartAsync()
    {
        if (_isConnected) return;

        try
        {
            _tcpClient = new TcpClient();
            StatusChanged?.Invoke($"Connecting to {Host}:{Port}...");

            await _tcpClient.ConnectAsync(Host, Port);
            _isConnected = true;
            _cts = new CancellationTokenSource();
            StatusChanged?.Invoke($"Connected to TCP {Host}:{Port}");

            _ = Task.Run(() => ReadWorker(_cts.Token));
        }
        catch (Exception ex)
        {
            _isConnected = false;
            ErrorOccurred?.Invoke(ex);
            StatusChanged?.Invoke($"Failed to connect to {Host}:{Port} - {ex.Message}");
        }
    }

    public Task StopAsync()
    {
        if (!_isConnected) return Task.CompletedTask;

        _isConnected = false;
        _cts?.Cancel();

        try
        {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }

        StatusChanged?.Invoke("TCP client disconnected");
        return Task.CompletedTask;
    }

    private async Task ReadWorker(CancellationToken token)
    {
        try
        {
            using var stream = _tcpClient!.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!token.IsCancellationRequested && _isConnected)
            {
                var line = await reader.ReadLineAsync(token);
                if (line == null) break;

                var evt = SerialTraceReceiver.ParseTraceLine(line);
                if (evt != null)
                {
                    EventReceived?.Invoke(evt);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(ex);
            }
        }
        finally
        {
            _isConnected = false;
        }
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }
}
