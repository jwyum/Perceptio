using System.IO.Ports;
using System.Text;
using Tracealyzer.Models;

namespace Tracealyzer.Services;

public class SerialTraceReceiver : ITraceDataSource
{
    private SerialPort? _serialPort;
    private CancellationTokenSource? _cts;
    private bool _isConnected;

    public string Name => $"Serial ({PortName} @ {BaudRate})";
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public bool IsConnected => _isConnected;

    public event Action<TraceEvent>? EventReceived;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? ErrorOccurred;

    public static string[] GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Task StartAsync()
    {
        if (_isConnected) return Task.CompletedTask;

        try
        {
            _serialPort = new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            _serialPort.Open();
            _isConnected = true;
            _cts = new CancellationTokenSource();
            StatusChanged?.Invoke($"Connected to {PortName} at {BaudRate} bps");

            Task.Run(() => ReadWorker(_cts.Token));
        }
        catch (Exception ex)
        {
            _isConnected = false;
            ErrorOccurred?.Invoke(ex);
            StatusChanged?.Invoke($"Failed to open {PortName}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_isConnected) return Task.CompletedTask;

        _isConnected = false;
        _cts?.Cancel();

        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
            _serialPort = null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }

        StatusChanged?.Invoke($"Disconnected from {PortName}");
        return Task.CompletedTask;
    }

    private void ReadWorker(CancellationToken token)
    {
        var lineBuffer = new StringBuilder();
        var buffer = new byte[2048];

        while (!token.IsCancellationRequested && _isConnected && _serialPort?.IsOpen == true)
        {
            try
            {
                int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0) continue;

                for (int i = 0; i < bytesRead; i++)
                {
                    char c = (char)buffer[i];
                    if (c == '\n' || c == '\r')
                    {
                        if (lineBuffer.Length > 0)
                        {
                            var line = lineBuffer.ToString().Trim();
                            lineBuffer.Clear();

                            var evt = ParseTraceLine(line);
                            if (evt != null)
                            {
                                EventReceived?.Invoke(evt);
                            }
                        }
                    }
                    else
                    {
                        lineBuffer.Append(c);
                    }
                }
            }
            catch (TimeoutException)
            {
                // Normal read timeout, continue
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    ErrorOccurred?.Invoke(ex);
                }
                break;
            }
        }
    }

    /// <summary>
    /// Parses line formats:
    /// Format 1: [TIMESTAMP_US],[EVENT_TYPE],[ACTOR_ID],[ACTOR_NAME],[PRIORITY],[DETAILS]
    /// e.g. "12450.5,TaskSwitchIn,2,T_Sensors,4,Running ADC sampling"
    /// </summary>
    public static TraceEvent? ParseTraceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
            return null;

        try
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3) return null;

            if (!double.TryParse(parts[0], out double timestampUs))
                return null;

            if (!Enum.TryParse<TraceEventType>(parts[1], true, out var eventType))
            {
                // Fallback fuzzy match
                eventType = parts[1].ToLowerInvariant() switch
                {
                    "in" or "switchin" => TraceEventType.TaskSwitchIn,
                    "out" or "switchout" => TraceEventType.TaskSwitchOut,
                    "isr_in" or "isrenter" => TraceEventType.IsrEnter,
                    "isr_out" or "isrexit" => TraceEventType.IsrExit,
                    "queue_send" => TraceEventType.QueueSend,
                    "queue_recv" => TraceEventType.QueueReceive,
                    "mutex_take" => TraceEventType.MutexTake,
                    "mutex_give" => TraceEventType.MutexGive,
                    "malloc" => TraceEventType.MemoryAlloc,
                    "free" => TraceEventType.MemoryFree,
                    _ => TraceEventType.UserEvent
                };
            }

            uint actorId = parts.Length > 2 && uint.TryParse(parts[2], out var aid) ? aid : 0;
            string actorName = parts.Length > 3 ? parts[3] : $"Actor_{actorId}";
            int priority = parts.Length > 4 && int.TryParse(parts[4], out var prio) ? prio : 1;
            string details = parts.Length > 5 ? string.Join(", ", parts.Skip(5)) : string.Empty;

            return new TraceEvent
            {
                TimestampUs = timestampUs,
                Type = eventType,
                ActorId = actorId,
                ActorName = actorName,
                Priority = priority,
                Details = details
            };
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }
}
