namespace Tracealyzer.Models;

public class TraceMarker
{
    public double TimestampUs { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#00ffff";
}

public class MeasurementState
{
    public double? Marker1Us { get; set; }
    public double? Marker2Us { get; set; }

    public bool HasMeasurement => Marker1Us.HasValue && Marker2Us.HasValue;

    public double DeltaUs => HasMeasurement ? Math.Abs(Marker2Us!.Value - Marker1Us!.Value) : 0;

    public double FrequencyHz => DeltaUs > 0 ? 1_000_000.0 / DeltaUs : 0;

    public string FormattedDelta
    {
        get
        {
            if (!HasMeasurement) return "N/A";
            if (DeltaUs < 1000)
                return $"{DeltaUs:F2} µs";
            if (DeltaUs < 1_000_000)
                return $"{DeltaUs / 1000.0:F3} ms";
            return $"{DeltaUs / 1_000_000.0:F6} s";
        }
    }

    public string FormattedFrequency
    {
        get
        {
            if (!HasMeasurement || FrequencyHz <= 0) return "N/A";
            if (FrequencyHz < 1000)
                return $"{FrequencyHz:F1} Hz";
            if (FrequencyHz < 1_000_000)
                return $"{FrequencyHz / 1000.0:F2} kHz";
            return $"{FrequencyHz / 1_000_000.0:F3} MHz";
        }
    }
}
