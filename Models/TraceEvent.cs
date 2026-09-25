namespace Tracealyzer.Models;

public class TraceEvent
{
    public long SequenceNumber { get; set; }
    public double TimestampUs { get; set; }
    public TraceEventType Type { get; set; }
    public uint ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Details { get; set; } = string.Empty;
    public uint ObjectHandle { get; set; }
    public long Argument { get; set; }

    public string FormattedTime
    {
        get
        {
            if (TimestampUs < 1000)
                return $"{TimestampUs:F2} µs";
            if (TimestampUs < 1_000_000)
                return $"{TimestampUs / 1000.0:F3} ms";
            return $"{TimestampUs / 1_000_000.0:F6} s";
        }
    }

    public string EventCategory => Type switch
    {
        TraceEventType.TaskSwitchIn or TraceEventType.TaskSwitchOut => "Scheduling",
        TraceEventType.TaskReady or TraceEventType.TaskSuspend or 
        TraceEventType.TaskResume or TraceEventType.TaskDelay or
        TraceEventType.TaskCreate or TraceEventType.TaskDelete => "Task State",
        TraceEventType.IsrEnter or TraceEventType.IsrExit => "Interrupt (ISR)",
        TraceEventType.QueueCreate or TraceEventType.QueueSend or 
        TraceEventType.QueueReceive or TraceEventType.QueueSendFailed or 
        TraceEventType.QueueReceiveFailed => "Queue",
        TraceEventType.MutexTake or TraceEventType.MutexGive or 
        TraceEventType.MutexTakeBlock or TraceEventType.SemaphoreGive or 
        TraceEventType.SemaphoreTake => "Sync / Mutex",
        TraceEventType.MemoryAlloc or TraceEventType.MemoryFree => "Memory",
        TraceEventType.UserEvent or TraceEventType.UserMarker => "User Event",
        _ => "General"
    };

    public string TypeDisplay => Type switch
    {
        TraceEventType.TaskSwitchIn => "TASK_SWITCH_IN",
        TraceEventType.TaskSwitchOut => "TASK_SWITCH_OUT",
        TraceEventType.TaskReady => "TASK_READY",
        TraceEventType.TaskSuspend => "TASK_SUSPEND",
        TraceEventType.TaskResume => "TASK_RESUME",
        TraceEventType.TaskDelay => "TASK_DELAY",
        TraceEventType.TaskCreate => "TASK_CREATE",
        TraceEventType.TaskDelete => "TASK_DELETE",
        TraceEventType.IsrEnter => "ISR_ENTER",
        TraceEventType.IsrExit => "ISR_EXIT",
        TraceEventType.QueueCreate => "QUEUE_CREATE",
        TraceEventType.QueueSend => "QUEUE_SEND",
        TraceEventType.QueueReceive => "QUEUE_RECEIVE",
        TraceEventType.QueueSendFailed => "QUEUE_SEND_BLOCK",
        TraceEventType.QueueReceiveFailed => "QUEUE_RECV_BLOCK",
        TraceEventType.MutexTake => "MUTEX_TAKE",
        TraceEventType.MutexGive => "MUTEX_GIVE",
        TraceEventType.MutexTakeBlock => "MUTEX_BLOCK",
        TraceEventType.SemaphoreGive => "SEM_GIVE",
        TraceEventType.SemaphoreTake => "SEM_TAKE",
        TraceEventType.MemoryAlloc => "MALLOC",
        TraceEventType.MemoryFree => "FREE",
        TraceEventType.UserEvent => "USER_LOG",
        TraceEventType.UserMarker => "USER_MARKER",
        _ => Type.ToString()
    };
}
