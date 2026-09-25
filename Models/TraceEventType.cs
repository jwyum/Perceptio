namespace Tracealyzer.Models;

public enum TraceEventType
{
    // Task Lifecycle & Scheduling
    TaskCreate,
    TaskReady,
    TaskSwitchIn,
    TaskSwitchOut,
    TaskSuspend,
    TaskResume,
    TaskDelay,
    TaskDelete,

    // ISR Interrupts
    IsrEnter,
    IsrExit,

    // Synchronization: Queues
    QueueCreate,
    QueueSend,
    QueueReceive,
    QueueSendFailed,
    QueueReceiveFailed,

    // Synchronization: Mutex & Semaphores
    MutexTake,
    MutexGive,
    MutexTakeBlock,
    SemaphoreGive,
    SemaphoreTake,

    // Memory
    MemoryAlloc,
    MemoryFree,

    // User Logs / Custom Markers
    UserEvent,
    UserMarker
}
