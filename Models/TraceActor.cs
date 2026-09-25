using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Tracealyzer.Models;

public class TraceActor : INotifyPropertyChanged
{
    private bool _isVisible = true;
    private double _cpuUtilizationPercent;
    private double _totalExecutionTimeUs;
    private int _switchCount;

    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ActorType Type { get; set; } = ActorType.Task;
    public int Priority { get; set; }
    public Color Color { get; set; }

    public SolidColorBrush Brush => new SolidColorBrush(Color);

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public double CpuUtilizationPercent
    {
        get => _cpuUtilizationPercent;
        set
        {
            if (Math.Abs(_cpuUtilizationPercent - value) > 0.001)
            {
                _cpuUtilizationPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuUtilizationDisplay));
            }
        }
    }

    public string CpuUtilizationDisplay => $"{_cpuUtilizationPercent:F1}%";

    public double TotalExecutionTimeUs
    {
        get => _totalExecutionTimeUs;
        set
        {
            if (Math.Abs(_totalExecutionTimeUs - value) > 0.001)
            {
                _totalExecutionTimeUs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalExecutionTimeDisplay));
            }
        }
    }

    public string TotalExecutionTimeDisplay
    {
        get
        {
            if (_totalExecutionTimeUs < 1000)
                return $"{_totalExecutionTimeUs:F1} µs";
            if (_totalExecutionTimeUs < 1_000_000)
                return $"{_totalExecutionTimeUs / 1000.0:F2} ms";
            return $"{_totalExecutionTimeUs / 1_000_000.0:F3} s";
        }
    }

    public int SwitchCount
    {
        get => _switchCount;
        set
        {
            if (_switchCount != value)
            {
                _switchCount = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
