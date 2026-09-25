using Tracealyzer.Models;

namespace Tracealyzer.Services;

public class TraceSimulationEngine : ITraceDataSource
{
    private CancellationTokenSource? _cts;
    private bool _isConnected;
    private double _currentTimestampUs;

    public string Name => "FreeRTOS RTOS Simulator";
    public bool IsConnected => _isConnected;

    public event Action<TraceEvent>? EventReceived;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? ErrorOccurred;

    /// <summary>
    /// Generates a complete snapshot session of specified duration (in milliseconds)
    /// </summary>
    public static TraceSession GenerateSnapshotSession(double durationMs = 3000)
    {
        var session = new TraceSession
        {
            Title = "FreeRTOS IoT Motor & Sensor Gateway - Snapshot",
            RtosName = "FreeRTOS Kernel V10.5.1",
            TickRateHz = 1000
        };

        var events = GenerateRealisticEventStream(0, durationMs * 1000);
        session.AddEvents(events);
        return session;
    }

    public Task StartAsync()
    {
        if (_isConnected) return Task.CompletedTask;

        _isConnected = true;
        _cts = new CancellationTokenSource();
        _currentTimestampUs = 0;
        StatusChanged?.Invoke("Simulator streaming live FreeRTOS events...");

        Task.Run(() => SimulationWorker(_cts.Token));
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_isConnected) return Task.CompletedTask;

        _isConnected = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        StatusChanged?.Invoke("Simulator stopped.");
        return Task.CompletedTask;
    }

    private async Task SimulationWorker(CancellationToken token)
    {
        try
        {
            var rand = new Random(42);
            while (!token.IsCancellationRequested)
            {
                // Generate chunk of 50ms of FreeRTOS activity
                double chunkDurationUs = 50_000;
                var chunkEvents = GenerateRealisticEventStream(_currentTimestampUs, _currentTimestampUs + chunkDurationUs, rand);
                _currentTimestampUs += chunkDurationUs;

                foreach (var evt in chunkEvents)
                {
                    if (token.IsCancellationRequested) break;
                    EventReceived?.Invoke(evt);
                }

                // Sleep ~50ms to match real-time
                await Task.Delay(50, token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            _isConnected = false;
        }
    }

    public static List<TraceEvent> GenerateRealisticEventStream(double startUs, double endUs, Random? random = null)
    {
        var rand = random ?? new Random(12345);
        var events = new List<TraceEvent>();

        // Actors definition
        var idIdle = 0u;
        var idMotor = 1u;
        var idSensor = 2u;
        var idComm = 3u;
        var idGui = 4u;
        var idLogger = 5u;
        var idIsrUart = 10u;
        var idIsrTick = 11u;

        // Periods in microseconds
        double periodMotor = 5000;      // 5 ms
        double periodSensor = 20000;    // 20 ms
        double periodComm = 50000;      // 50 ms
        double periodGui = 33333;       // ~30 FPS
        double periodLogger = 100000;   // 100 ms
        double periodSysTick = 1000;    // 1 ms

        double time = startUs;

        // Initial task creations if starting from 0
        if (startUs < 1)
        {
            events.Add(new TraceEvent { TimestampUs = 0, Type = TraceEventType.TaskCreate, ActorId = idIdle, ActorName = "IDLE", Priority = 0, Details = "Created Idle Task" });
            events.Add(new TraceEvent { TimestampUs = 10, Type = TraceEventType.TaskCreate, ActorId = idMotor, ActorName = "T_MotorCtrl", Priority = 5, Details = "High-priority Motor Loop" });
            events.Add(new TraceEvent { TimestampUs = 20, Type = TraceEventType.TaskCreate, ActorId = idSensor, ActorName = "T_Sensors", Priority = 4, Details = "Sensor Acquisition Task" });
            events.Add(new TraceEvent { TimestampUs = 30, Type = TraceEventType.TaskCreate, ActorId = idComm, ActorName = "T_Network", Priority = 3, Details = "MQTT & Telemetry Comm" });
            events.Add(new TraceEvent { TimestampUs = 40, Type = TraceEventType.TaskCreate, ActorId = idGui, ActorName = "T_Display", Priority = 2, Details = "LVGL UI Frame Renderer" });
            events.Add(new TraceEvent { TimestampUs = 50, Type = TraceEventType.TaskCreate, ActorId = idLogger, ActorName = "T_Storage", Priority = 1, Details = "Flash Wear & Diagnostics" });
            
            // Queues and Mutex creation
            events.Add(new TraceEvent { TimestampUs = 60, Type = TraceEventType.QueueCreate, ActorId = idSensor, ActorName = "T_Sensors", Details = "Created Queue [xQueueSensorData, len=16]" });
            events.Add(new TraceEvent { TimestampUs = 70, Type = TraceEventType.MutexTake, ActorId = idComm, ActorName = "T_Network", Details = "Created Mutex [xSemaphoreSPI]" });
            events.Add(new TraceEvent { TimestampUs = 80, Type = TraceEventType.MutexGive, ActorId = idComm, ActorName = "T_Network", Details = "Initialized Mutex [xSemaphoreSPI]" });
            time = 100;
        }

        uint currentRunningTask = idIdle;
        string currentTaskName = "IDLE";
        int currentPriority = 0;

        void SwitchTask(uint newId, string newName, int priority, double atTime, string reason)
        {
            if (currentRunningTask == newId) return;

            // Switch out current
            events.Add(new TraceEvent
            {
                TimestampUs = atTime,
                Type = TraceEventType.TaskSwitchOut,
                ActorId = currentRunningTask,
                ActorName = currentTaskName,
                Priority = currentPriority,
                Details = $"Switch out -> {reason}"
            });

            // Switch in new
            currentRunningTask = newId;
            currentTaskName = newName;
            currentPriority = priority;

            events.Add(new TraceEvent
            {
                TimestampUs = atTime,
                Type = TraceEventType.TaskSwitchIn,
                ActorId = currentRunningTask,
                ActorName = currentTaskName,
                Priority = currentPriority,
                Details = $"Switch in"
            });
        }

        void RunIsr(uint isrId, string isrName, double atTime, double durationUs, string details)
        {
            events.Add(new TraceEvent
            {
                TimestampUs = atTime,
                Type = TraceEventType.IsrEnter,
                ActorId = isrId,
                ActorName = isrName,
                Priority = 12,
                Details = details
            });

            events.Add(new TraceEvent
            {
                TimestampUs = atTime + durationUs,
                Type = TraceEventType.IsrExit,
                ActorId = isrId,
                ActorName = isrName,
                Priority = 12,
                Details = "ISR return to task"
            });
        }

        // Stepping loop through time
        double nextSysTick = Math.Ceiling(time / periodSysTick) * periodSysTick;
        double nextMotor = Math.Ceiling(time / periodMotor) * periodMotor;
        double nextSensor = Math.Ceiling(time / periodSensor) * periodSensor;
        double nextComm = Math.Ceiling(time / periodComm) * periodComm;
        double nextGui = Math.Ceiling(time / periodGui) * periodGui;
        double nextLogger = Math.Ceiling(time / periodLogger) * periodLogger;
        double nextUart = time + rand.Next(8000, 15000);

        while (time < endUs)
        {
            // Check SysTick
            if (time >= nextSysTick)
            {
                RunIsr(idIsrTick, "SysTick_ISR", nextSysTick, 18, "FreeRTOS Kernel Tick (1ms)");
                nextSysTick += periodSysTick;
            }

            // Check UART Interrupt
            if (time >= nextUart)
            {
                RunIsr(idIsrUart, "UART_Rx_ISR", nextUart, 45, "Rx buffer full: received packet header [0xAA 0x55]");
                events.Add(new TraceEvent
                {
                    TimestampUs = nextUart + 46,
                    Type = TraceEventType.SemaphoreGive,
                    ActorId = idIsrUart,
                    ActorName = "UART_Rx_ISR",
                    Priority = 12,
                    Details = "xSemaphoreGiveFromISR(xRxReadySem)"
                });
                nextUart = time + rand.Next(12000, 25000);
            }

            // Motor Control Task (Period 5ms, Execution 350us)
            if (time >= nextMotor)
            {
                SwitchTask(idMotor, "T_MotorCtrl", 5, nextMotor, "Periodic timer wake");
                time = nextMotor + 380;
                events.Add(new TraceEvent
                {
                    TimestampUs = time - 50,
                    Type = TraceEventType.UserEvent,
                    ActorId = idMotor,
                    ActorName = "T_MotorCtrl",
                    Priority = 5,
                    Details = $"PID Update: RPM={2400 + rand.Next(-25, 25)}, PWM={68 + rand.Next(-3, 3)}%"
                });
                SwitchTask(idIdle, "IDLE", 0, time, "vTaskDelay(5)");
                nextMotor += periodMotor;
                continue;
            }

            // Sensor Task (Period 20ms, Execution 1.2ms)
            if (time >= nextSensor)
            {
                SwitchTask(idSensor, "T_Sensors", 4, nextSensor, "Period 20ms sensor read");
                
                // Sensor queue send
                events.Add(new TraceEvent
                {
                    TimestampUs = nextSensor + 400,
                    Type = TraceEventType.QueueSend,
                    ActorId = idSensor,
                    ActorName = "T_Sensors",
                    Priority = 4,
                    Details = "xQueueSend(xQueueSensorData, Temp=24.8C, Press=1013hPa)"
                });

                // Preempted by Motor Task if motor occurs during sensor
                time = nextSensor + 1200;
                SwitchTask(idIdle, "IDLE", 0, time, "Sensor read complete");
                nextSensor += periodSensor;
                continue;
            }

            // Network Comm Task (Period 50ms, Execution 2.8ms, Mutex acquisition)
            if (time >= nextComm)
            {
                SwitchTask(idComm, "T_Network", 3, nextComm, "Network packet send");
                
                // Mutex Lock
                events.Add(new TraceEvent
                {
                    TimestampUs = nextComm + 200,
                    Type = TraceEventType.MutexTake,
                    ActorId = idComm,
                    ActorName = "T_Network",
                    Priority = 3,
                    Details = "xSemaphoreTake(xSemaphoreSPI, portMAX_DELAY) -> Acquired"
                });

                events.Add(new TraceEvent
                {
                    TimestampUs = nextComm + 600,
                    Type = TraceEventType.QueueReceive,
                    ActorId = idComm,
                    ActorName = "T_Network",
                    Priority = 3,
                    Details = "xQueueReceive(xQueueSensorData) -> 32 bytes read"
                });

                events.Add(new TraceEvent
                {
                    TimestampUs = nextComm + 1500,
                    Type = TraceEventType.MutexGive,
                    ActorId = idComm,
                    ActorName = "T_Network",
                    Priority = 3,
                    Details = "xSemaphoreGive(xSemaphoreSPI) -> Released"
                });

                time = nextComm + 2800;
                SwitchTask(idIdle, "IDLE", 0, time, "Packet sent to broker");
                nextComm += periodComm;
                continue;
            }

            // GUI Display Task (Period 33ms, Execution 4ms)
            if (time >= nextGui)
            {
                SwitchTask(idGui, "T_Display", 2, nextGui, "Display frame render");
                
                events.Add(new TraceEvent
                {
                    TimestampUs = nextGui + 300,
                    Type = TraceEventType.MutexTake,
                    ActorId = idGui,
                    ActorName = "T_Display",
                    Priority = 2,
                    Details = "xSemaphoreTake(xSemaphoreSPI, 10ms) -> Acquired"
                });

                events.Add(new TraceEvent
                {
                    TimestampUs = nextGui + 2400,
                    Type = TraceEventType.MutexGive,
                    ActorId = idGui,
                    ActorName = "T_Display",
                    Priority = 2,
                    Details = "xSemaphoreGive(xSemaphoreSPI)"
                });

                time = nextGui + 3800;
                SwitchTask(idIdle, "IDLE", 0, time, "Frame complete (60 FPS)");
                nextGui += periodGui;
                continue;
            }

            // Storage Logger Task (Period 100ms, Execution 5.5ms)
            if (time >= nextLogger)
            {
                SwitchTask(idLogger, "T_Storage", 1, nextLogger, "Periodic diagnostic flush");
                
                events.Add(new TraceEvent
                {
                    TimestampUs = nextLogger + 100,
                    Type = TraceEventType.MemoryAlloc,
                    ActorId = idLogger,
                    ActorName = "T_Storage",
                    Priority = 1,
                    Argument = 1024,
                    Details = "pvPortMalloc(1024) -> Buffer allocated at 0x20014000"
                });

                events.Add(new TraceEvent
                {
                    TimestampUs = nextLogger + 2500,
                    Type = TraceEventType.UserEvent,
                    ActorId = idLogger,
                    ActorName = "T_Storage",
                    Priority = 1,
                    Details = "Flash Sector Erase & Write: 4096 bytes committed"
                });

                events.Add(new TraceEvent
                {
                    TimestampUs = nextLogger + 4500,
                    Type = TraceEventType.MemoryFree,
                    ActorId = idLogger,
                    ActorName = "T_Storage",
                    Priority = 1,
                    Argument = 1024,
                    Details = "vPortFree(0x20014000) -> Buffer freed"
                });

                time = nextLogger + 5200;
                SwitchTask(idIdle, "IDLE", 0, time, "Flash sync complete");
                nextLogger += periodLogger;
                continue;
            }

            // Idle run advance
            double nextEventTime = Math.Min(nextSysTick, Math.Min(nextMotor, Math.Min(nextSensor, Math.Min(nextComm, Math.Min(nextGui, nextLogger)))));
            if (nextEventTime <= time)
            {
                nextEventTime = time + 100;
            }

            if (currentRunningTask != idIdle)
            {
                SwitchTask(idIdle, "IDLE", 0, time, "Idle loop waiting");
            }

            time = nextEventTime;
        }

        // Sort events chronologically to guarantee strict monotonic order
        events.Sort((a, b) => a.TimestampUs.CompareTo(b.TimestampUs));
        return events;
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }
}
