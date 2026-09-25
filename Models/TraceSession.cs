using System.Collections.Concurrent;
using System.Windows.Media;

namespace Tracealyzer.Models;

public class TraceSession
{
    private readonly object _lock = new();
    private readonly List<TraceEvent> _events = new();
    private readonly Dictionary<uint, TraceActor> _actors = new();
    private readonly List<ExecutionInterval> _intervals = new();

    // Stack to track active execution (to handle ISR nesting and preemption)
    private readonly Stack<(TraceActor Actor, double StartUs, TraceEvent StartEvt)> _activeExecStack = new();

    public string Title { get; set; } = "FreeRTOS Session";
    public string RtosName { get; set; } = "FreeRTOS v10.5.1";
    public int TickRateHz { get; set; } = 1000;
    public double DurationUs { get; private set; }
    public double MaxTimestampUs { get; private set; }
    public double MinTimestampUs { get; private set; }

    public IReadOnlyList<TraceEvent> Events
    {
        get { lock (_lock) return _events.ToArray(); }
    }

    public IReadOnlyDictionary<uint, TraceActor> Actors
    {
        get { lock (_lock) return new Dictionary<uint, TraceActor>(_actors); }
    }

    public IReadOnlyList<ExecutionInterval> Intervals
    {
        get { lock (_lock) return _intervals.ToArray(); }
    }

    public event Action? SessionUpdated;

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
            _actors.Clear();
            _intervals.Clear();
            _activeExecStack.Clear();
            DurationUs = 0;
            MinTimestampUs = 0;
            MaxTimestampUs = 0;
        }
        SessionUpdated?.Invoke();
    }

    public TraceActor GetOrAddActor(uint id, string name, ActorType type, int priority)
    {
        lock (_lock)
        {
            if (!_actors.TryGetValue(id, out var actor))
            {
                actor = new TraceActor
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    Priority = priority,
                    Color = GetActorColor(id, type)
                };
                _actors[id] = actor;
            }
            return actor;
        }
    }

    public void AddEvent(TraceEvent evt)
    {
        lock (_lock)
        {
            if (_events.Count == 0)
            {
                MinTimestampUs = evt.TimestampUs;
            }

            evt.SequenceNumber = _events.Count + 1;
            _events.Add(evt);

            if (evt.TimestampUs > MaxTimestampUs)
            {
                MaxTimestampUs = evt.TimestampUs;
                DurationUs = MaxTimestampUs - MinTimestampUs;
            }

            // Process scheduling intervals
            ProcessSchedulingEvent(evt);
        }
    }

    public void AddEvents(IEnumerable<TraceEvent> events)
    {
        lock (_lock)
        {
            foreach (var evt in events)
            {
                if (_events.Count == 0)
                {
                    MinTimestampUs = evt.TimestampUs;
                }

                evt.SequenceNumber = _events.Count + 1;
                _events.Add(evt);

                if (evt.TimestampUs > MaxTimestampUs)
                {
                    MaxTimestampUs = evt.TimestampUs;
                    DurationUs = MaxTimestampUs - MinTimestampUs;
                }

                ProcessSchedulingEvent(evt);
            }
            RecalculateStatisticsNoLock();
        }
        SessionUpdated?.Invoke();
    }

    private void ProcessSchedulingEvent(TraceEvent evt)
    {
        var actor = GetOrAddActor(evt.ActorId, evt.ActorName, 
            evt.Type == TraceEventType.IsrEnter || evt.Type == TraceEventType.IsrExit ? ActorType.Isr : 
            evt.ActorName.Contains("IDLE", StringComparison.OrdinalIgnoreCase) ? ActorType.Idle : ActorType.Task,
            evt.Priority);

        switch (evt.Type)
        {
            case TraceEventType.TaskSwitchIn:
                // If another task was running without switch-out, finish it
                while (_activeExecStack.Count > 0 && _activeExecStack.Peek().Actor.Type != ActorType.Isr)
                {
                    var prev = _activeExecStack.Pop();
                    CloseInterval(prev.Actor, prev.StartUs, evt.TimestampUs, prev.StartEvt, evt, isPreempted: true, preemptedBy: actor.Name);
                }
                _activeExecStack.Push((actor, evt.TimestampUs, evt));
                break;

            case TraceEventType.TaskSwitchOut:
                if (_activeExecStack.Count > 0 && _activeExecStack.Peek().Actor.Id == actor.Id)
                {
                    var active = _activeExecStack.Pop();
                    CloseInterval(active.Actor, active.StartUs, evt.TimestampUs, active.StartEvt, evt, isPreempted: false, string.Empty);
                }
                break;

            case TraceEventType.IsrEnter:
                // Preempt whatever is currently running and push ISR
                _activeExecStack.Push((actor, evt.TimestampUs, evt));
                break;

            case TraceEventType.IsrExit:
                if (_activeExecStack.Count > 0 && _activeExecStack.Peek().Actor.Id == actor.Id)
                {
                    var isr = _activeExecStack.Pop();
                    CloseInterval(isr.Actor, isr.StartUs, evt.TimestampUs, isr.StartEvt, evt, isPreempted: false, string.Empty);
                }
                break;
        }
    }

    private void CloseInterval(TraceActor actor, double startUs, double endUs, TraceEvent startEvt, TraceEvent endEvt, bool isPreempted, string preemptedBy)
    {
        if (endUs <= startUs) return;

        var interval = new ExecutionInterval
        {
            Actor = actor,
            StartUs = startUs,
            EndUs = endUs,
            IsPreempted = isPreempted,
            PreemptedBy = preemptedBy,
            StartEvent = startEvt,
            EndEvent = endEvt
        };

        _intervals.Add(interval);
        actor.SwitchCount++;
    }

    public void RecalculateStatistics()
    {
        lock (_lock)
        {
            RecalculateStatisticsNoLock();
        }
        SessionUpdated?.Invoke();
    }

    private void RecalculateStatisticsNoLock()
    {
        var totalSessionTime = Math.Max(1.0, DurationUs);
        var actorExecutionMap = new Dictionary<uint, double>();

        foreach (var interval in _intervals)
        {
            if (!actorExecutionMap.ContainsKey(interval.Actor.Id))
                actorExecutionMap[interval.Actor.Id] = 0;
            actorExecutionMap[interval.Actor.Id] += interval.DurationUs;
        }

        foreach (var actor in _actors.Values)
        {
            if (actorExecutionMap.TryGetValue(actor.Id, out var execUs))
            {
                actor.TotalExecutionTimeUs = execUs;
                actor.CpuUtilizationPercent = (execUs / totalSessionTime) * 100.0;
            }
            else
            {
                actor.TotalExecutionTimeUs = 0;
                actor.CpuUtilizationPercent = 0;
            }
        }
    }

    /// <summary>
    /// Binary search for intervals overlapping [startUs, endUs] for 60fps rendering
    /// </summary>
    public List<ExecutionInterval> GetIntervalsInRange(double startUs, double endUs)
    {
        lock (_lock)
        {
            var result = new List<ExecutionInterval>();
            if (_intervals.Count == 0 || endUs < _intervals[0].StartUs || startUs > _intervals[^1].EndUs)
                return result;

            // Binary search for first interval that might overlap
            int low = 0, high = _intervals.Count - 1;
            int startIndex = 0;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_intervals[mid].EndUs >= startUs)
                {
                    startIndex = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            for (int i = startIndex; i < _intervals.Count; i++)
            {
                var item = _intervals[i];
                if (item.StartUs > endUs)
                    break;
                if (item.EndUs >= startUs)
                {
                    result.Add(item);
                }
            }

            return result;
        }
    }

    private static Color GetActorColor(uint id, ActorType type)
    {
        if (type == ActorType.Idle)
            return Color.FromRgb(80, 85, 95); // Subtle dark slate for idle

        // Distinct, professional Tracealyzer palette
        var palette = new[]
        {
            Color.FromRgb(0, 180, 216),    // Ocean Cyan
            Color.FromRgb(16, 185, 129),   // Emerald Green
            Color.FromRgb(245, 158, 11),   // Amber
            Color.FromRgb(139, 92, 246),   // Royal Violet
            Color.FromRgb(239, 68, 68),    // Coral Red
            Color.FromRgb(20, 184, 166),   // Teal
            Color.FromRgb(236, 72, 153),   // Vivid Pink
            Color.FromRgb(99, 102, 241),   // Indigo
            Color.FromRgb(132, 204, 22),   // Lime Green
            Color.FromRgb(249, 115, 22),   // Orange
            Color.FromRgb(14, 165, 233),   // Sky Blue
            Color.FromRgb(168, 85, 247)    // Fuchsia
        };

        return palette[(int)(id % palette.Length)];
    }
}
