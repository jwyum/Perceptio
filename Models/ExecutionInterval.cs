namespace Tracealyzer.Models;

public class ExecutionInterval
{
    public TraceActor Actor { get; set; } = null!;
    public double StartUs { get; set; }
    public double EndUs { get; set; }
    public double DurationUs => Math.Max(0, EndUs - StartUs);

    public bool IsIsr => Actor.Type == ActorType.Isr;
    public bool IsIdle => Actor.Type == ActorType.Idle;

    public bool IsPreempted { get; set; }
    public string PreemptedBy { get; set; } = string.Empty;

    public TraceEvent? StartEvent { get; set; }
    public TraceEvent? EndEvent { get; set; }

    public string FormattedDuration
    {
        get
        {
            if (DurationUs < 1000)
                return $"{DurationUs:F1} µs";
            if (DurationUs < 1_000_000)
                return $"{DurationUs / 1000.0:F3} ms";
            return $"{DurationUs / 1_000_000.0:F4} s";
        }
    }
}
