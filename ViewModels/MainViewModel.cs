using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Tracealyzer.Models;
using Tracealyzer.Services;

namespace Tracealyzer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly Dispatcher _dispatcher;
    private TraceSession _session = new();
    private ITraceDataSource? _activeSource;
    private bool _isLiveStreaming;
    private bool _followLive = true;
    private string _statusMessage = "Ready. Load a trace file, connect to hardware, or click 'Run FreeRTOS Demo'.";

    // Viewport coordinates in microseconds
    private double _viewportStartUs;
    private double _viewportEndUs = 100_000; // default 100ms window

    // Selection
    private TraceEvent? _selectedEvent;
    private ExecutionInterval? _selectedInterval;
    private TraceActor? _selectedActor;

    // Measurement tool
    private readonly MeasurementState _measurement = new();

    // Filters
    private string _eventSearchText = string.Empty;
    private string _selectedCategoryFilter = "All";

    // Live streaming settings
    private string _selectedComPort = "COM1";
    private int _selectedBaudRate = 115200;
    private string _tcpHost = "127.0.0.1";
    private int _tcpPort = 50000;

    public MainViewModel()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        
        // Commands
        LoadDemoTraceCommand = new RelayCommand(LoadDemoTrace);
        StartSimulationStreamCommand = new RelayCommand(ToggleSimulationStream);
        OpenFileCommand = new RelayCommand(OpenFile);
        SaveTraceCommand = new RelayCommand(SaveTrace, () => _session.Events.Count > 0);
        ClearSessionCommand = new RelayCommand(ClearSession);
        ZoomInCommand = new RelayCommand(() => Zoom(0.7));
        ZoomOutCommand = new RelayCommand(() => Zoom(1.4));
        ZoomFitCommand = new RelayCommand(ZoomToFit);
        ToggleFollowLiveCommand = new RelayCommand(() => FollowLive = !FollowLive);
        ClearMeasurementCommand = new RelayCommand(ClearMeasurement);
        ConnectSerialCommand = new RelayCommand(ToggleSerialConnection);
        ConnectTcpCommand = new RelayCommand(ToggleTcpConnection);

        // Populate COM ports
        RefreshComPorts();

        // Load demo data right away so user sees instant results!
        LoadDemoTrace();
    }

    public TraceSession Session
    {
        get => _session;
        private set
        {
            if (_session != null)
            {
                _session.SessionUpdated -= OnSessionUpdated;
            }
            _session = value;
            if (_session != null)
            {
                _session.SessionUpdated += OnSessionUpdated;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActorsList));
            OnPropertyChanged(nameof(TotalDurationDisplay));
            OnPropertyChanged(nameof(EventCountDisplay));
            SetupFilteredEvents();
        }
    }

    public ObservableCollection<TraceActor> ActorsList => new(_session.Actors.Values);

    private ICollectionView? _filteredEventsView;
    public ICollectionView? FilteredEvents => _filteredEventsView;

    public MeasurementState Measurement => _measurement;

    public double ViewportStartUs
    {
        get => _viewportStartUs;
        set
        {
            if (SetProperty(ref _viewportStartUs, value))
            {
                OnPropertyChanged(nameof(ViewportDurationUs));
                OnPropertyChanged(nameof(ViewportTimeRangeDisplay));
                TimelineRangeChanged?.Invoke();
            }
        }
    }

    public double ViewportEndUs
    {
        get => _viewportEndUs;
        set
        {
            if (SetProperty(ref _viewportEndUs, value))
            {
                OnPropertyChanged(nameof(ViewportDurationUs));
                OnPropertyChanged(nameof(ViewportTimeRangeDisplay));
                TimelineRangeChanged?.Invoke();
            }
        }
    }

    public double ViewportDurationUs => Math.Max(1.0, _viewportEndUs - _viewportStartUs);

    public string ViewportTimeRangeDisplay
    {
        get
        {
            string startStr = FormatTimeUs(_viewportStartUs);
            string endStr = FormatTimeUs(_viewportEndUs);
            string spanStr = FormatTimeUs(ViewportDurationUs);
            return $"{startStr} -> {endStr} (Window: {spanStr})";
        }
    }

    public bool FollowLive
    {
        get => _followLive;
        set => SetProperty(ref _followLive, value);
    }

    public bool IsLiveStreaming
    {
        get => _isLiveStreaming;
        set
        {
            if (SetProperty(ref _isLiveStreaming, value))
            {
                OnPropertyChanged(nameof(StreamButtonLabel));
                OnPropertyChanged(nameof(CanChangeSourceSettings));
            }
        }
    }

    public string StreamButtonLabel => IsLiveStreaming ? "Stop Live Stream" : "Live Simulation Stream";
    public bool CanChangeSourceSettings => !IsLiveStreaming;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public TraceEvent? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (SetProperty(ref _selectedEvent, value) && value != null)
            {
                // Auto center viewport around selected event if out of view
                if (value.TimestampUs < _viewportStartUs || value.TimestampUs > _viewportEndUs)
                {
                    CenterOnTimestamp(value.TimestampUs);
                }
            }
        }
    }

    public ExecutionInterval? SelectedInterval
    {
        get => _selectedInterval;
        set => SetProperty(ref _selectedInterval, value);
    }

    public TraceActor? SelectedActor
    {
        get => _selectedActor;
        set => SetProperty(ref _selectedActor, value);
    }

    public string EventSearchText
    {
        get => _eventSearchText;
        set
        {
            if (SetProperty(ref _eventSearchText, value))
            {
                _filteredEventsView?.Refresh();
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                _filteredEventsView?.Refresh();
            }
        }
    }

    public ObservableCollection<string> AvailableComPorts { get; } = new();
    public int[] AvailableBaudRates { get; } = [ 115200, 230400, 460800, 921600, 1000000, 2000000 ];
    public string[] AvailableCategories { get; } = [ "All", "Scheduling", "Interrupt (ISR)", "Queue", "Sync / Mutex", "Memory", "User Event" ];

    public string SelectedComPort
    {
        get => _selectedComPort;
        set => SetProperty(ref _selectedComPort, value);
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    public string TcpHost
    {
        get => _tcpHost;
        set => SetProperty(ref _tcpHost, value);
    }

    public int TcpPort
    {
        get => _tcpPort;
        set => SetProperty(ref _tcpPort, value);
    }

    public string TotalDurationDisplay => FormatTimeUs(_session.DurationUs);
    public string EventCountDisplay => $"{_session.Events.Count:N0} events";
    public string IntervalCountDisplay => $"{_session.Intervals.Count:N0} slices";

    // Events
    public event Action? TimelineRangeChanged;
    public event Action? TimelineRepaintRequested;

    // Commands declaration
    public ICommand LoadDemoTraceCommand { get; }
    public ICommand StartSimulationStreamCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand SaveTraceCommand { get; }
    public ICommand ClearSessionCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ZoomFitCommand { get; }
    public ICommand ToggleFollowLiveCommand { get; }
    public ICommand ClearMeasurementCommand { get; }
    public ICommand ConnectSerialCommand { get; }
    public ICommand ConnectTcpCommand { get; }

    public void RefreshComPorts()
    {
        AvailableComPorts.Clear();
        foreach (var port in SerialTraceReceiver.GetAvailablePorts())
        {
            AvailableComPorts.Add(port);
        }
        if (AvailableComPorts.Count > 0 && !AvailableComPorts.Contains(SelectedComPort))
        {
            SelectedComPort = AvailableComPorts[0];
        }
    }

    public async void LoadDemoTrace()
    {
        await StopLiveStream();
        StatusMessage = "Generating realistic FreeRTOS trace demonstration...";

        await Task.Run(() =>
        {
            var demoSession = TraceSimulationEngine.GenerateSnapshotSession(3000); // 3 seconds
            _dispatcher.Invoke(() =>
            {
                Session = demoSession;
                ZoomToFit();
                StatusMessage = $"FreeRTOS Trace Loaded: {Session.Events.Count:N0} events, {Session.Actors.Count} actors across {FormatTimeUs(Session.DurationUs)}";
            });
        });
    }

    public async void ToggleSimulationStream()
    {
        if (IsLiveStreaming)
        {
            await StopLiveStream();
        }
        else
        {
            ClearSession();
            var sim = new TraceSimulationEngine();
            await StartSourceAsync(sim);
        }
    }

    public async void ToggleSerialConnection()
    {
        if (IsLiveStreaming && _activeSource is SerialTraceReceiver)
        {
            await StopLiveStream();
        }
        else
        {
            await StopLiveStream();
            var serial = new SerialTraceReceiver
            {
                PortName = SelectedComPort,
                BaudRate = SelectedBaudRate
            };
            await StartSourceAsync(serial);
        }
    }

    public async void ToggleTcpConnection()
    {
        if (IsLiveStreaming && _activeSource is TcpTraceReceiver)
        {
            await StopLiveStream();
        }
        else
        {
            await StopLiveStream();
            var tcp = new TcpTraceReceiver
            {
                Host = TcpHost,
                Port = TcpPort
            };
            await StartSourceAsync(tcp);
        }
    }

    private async Task StartSourceAsync(ITraceDataSource source)
    {
        _activeSource = source;
        _activeSource.EventReceived += OnStreamEventReceived;
        _activeSource.StatusChanged += msg => _dispatcher.Invoke(() => StatusMessage = msg);
        _activeSource.ErrorOccurred += ex => _dispatcher.Invoke(() => StatusMessage = $"Error: {ex.Message}");

        IsLiveStreaming = true;
        await _activeSource.StartAsync();
    }

    public async Task StopLiveStream()
    {
        if (_activeSource != null)
        {
            await _activeSource.StopAsync();
            _activeSource.EventReceived -= OnStreamEventReceived;
            _activeSource.Dispose();
            _activeSource = null;
        }
        IsLiveStreaming = false;
        StatusMessage = "Live stream stopped.";
    }

    private readonly List<TraceEvent> _incomingBatch = new();
    private DateTime _lastBatchFlush = DateTime.MinValue;

    private void OnStreamEventReceived(TraceEvent evt)
    {
        lock (_incomingBatch)
        {
            _incomingBatch.Add(evt);
        }

        // Batch flush every 50ms to UI thread for smooth 60fps rendering without overhead
        if ((DateTime.UtcNow - _lastBatchFlush).TotalMilliseconds > 50)
        {
            _lastBatchFlush = DateTime.UtcNow;
            _dispatcher.BeginInvoke(() =>
            {
                List<TraceEvent> toAdd;
                lock (_incomingBatch)
                {
                    toAdd = new List<TraceEvent>(_incomingBatch);
                    _incomingBatch.Clear();
                }

                if (toAdd.Count > 0)
                {
                    _session.AddEvents(toAdd);

                    if (FollowLive)
                    {
                        double window = ViewportDurationUs;
                        ViewportEndUs = _session.MaxTimestampUs;
                        ViewportStartUs = Math.Max(0, ViewportEndUs - window);
                    }
                }
            });
        }
    }

    public async void OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "FreeRTOS Trace Files (*.json, *.csv, *.bin)|*.json;*.csv;*.txt;*.bin|JSON Trace (*.json)|*.json|CSV Trace (*.csv)|*.csv|Binary Dump (*.bin)|*.bin|All Files (*.*)|*.*",
            Title = "Open FreeRTOS Trace File"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                await StopLiveStream();
                StatusMessage = $"Loading {Path.GetFileName(dlg.FileName)}...";
                var session = await SnapshotFileParser.LoadFromFileAsync(dlg.FileName);
                Session = session;
                ZoomToFit();
                StatusMessage = $"Loaded {Session.Events.Count:N0} events from {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load trace: {ex.Message}", "Open Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Error loading trace file.";
            }
        }
    }

    public async void SaveTrace()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON Trace (*.json)|*.json",
            FileName = $"FreeRTOS_Trace_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            Title = "Save FreeRTOS Trace Session"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                StatusMessage = "Saving trace session...";
                await SnapshotFileParser.SaveToJsonAsync(_session, dlg.FileName);
                StatusMessage = $"Trace saved to {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save trace: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void ClearSession()
    {
        _session.Clear();
        ViewportStartUs = 0;
        ViewportEndUs = 100_000;
        ClearMeasurement();
        SelectedEvent = null;
        SelectedInterval = null;
        SelectedActor = null;
        StatusMessage = "Session cleared.";
        OnPropertyChanged(nameof(ActorsList));
        OnPropertyChanged(nameof(TotalDurationDisplay));
        OnPropertyChanged(nameof(EventCountDisplay));
        OnPropertyChanged(nameof(IntervalCountDisplay));
        TimelineRepaintRequested?.Invoke();
    }

    public void Zoom(double factor, double? centerTimeUs = null)
    {
        double currentDuration = ViewportDurationUs;
        double newDuration = Math.Clamp(currentDuration * factor, 10.0, Math.Max(100_000.0, _session.DurationUs * 1.5));
        double center = centerTimeUs ?? (_viewportStartUs + currentDuration / 2.0);

        double ratio = (center - _viewportStartUs) / currentDuration;
        double newStart = center - newDuration * ratio;
        double newEnd = newStart + newDuration;

        if (newStart < 0)
        {
            newStart = 0;
            newEnd = newDuration;
        }

        ViewportStartUs = newStart;
        ViewportEndUs = newEnd;
    }

    public void ZoomToFit()
    {
        if (_session.Events.Count == 0)
        {
            ViewportStartUs = 0;
            ViewportEndUs = 100_000;
            return;
        }

        ViewportStartUs = Math.Max(0, _session.MinTimestampUs);
        ViewportEndUs = Math.Max(1000, _session.MaxTimestampUs);
    }

    public void CenterOnTimestamp(double timestampUs)
    {
        double curWindow = ViewportDurationUs;
        double newStart = Math.Max(0, timestampUs - curWindow / 2.0);
        ViewportStartUs = newStart;
        ViewportEndUs = newStart + curWindow;
    }

    public void SetMeasurementMarker1(double timeUs)
    {
        _measurement.Marker1Us = timeUs;
        OnPropertyChanged(nameof(Measurement));
        TimelineRepaintRequested?.Invoke();
    }

    public void SetMeasurementMarker2(double timeUs)
    {
        _measurement.Marker2Us = timeUs;
        OnPropertyChanged(nameof(Measurement));
        TimelineRepaintRequested?.Invoke();
    }

    public void ClearMeasurement()
    {
        _measurement.Marker1Us = null;
        _measurement.Marker2Us = null;
        OnPropertyChanged(nameof(Measurement));
        TimelineRepaintRequested?.Invoke();
    }

    private void SetupFilteredEvents()
    {
        _filteredEventsView = CollectionViewSource.GetDefaultView(_session.Events);
        if (_filteredEventsView != null)
        {
            _filteredEventsView.Filter = FilterEventPredicate;
        }
        OnPropertyChanged(nameof(FilteredEvents));
    }

    private bool FilterEventPredicate(object obj)
    {
        if (obj is not TraceEvent evt) return false;

        // Category filter
        if (_selectedCategoryFilter != "All" && evt.EventCategory != _selectedCategoryFilter)
            return false;

        // Text search
        if (!string.IsNullOrWhiteSpace(_eventSearchText))
        {
            return evt.ActorName.Contains(_eventSearchText, StringComparison.OrdinalIgnoreCase) ||
                   evt.Details.Contains(_eventSearchText, StringComparison.OrdinalIgnoreCase) ||
                   evt.TypeDisplay.Contains(_eventSearchText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private void OnSessionUpdated()
    {
        OnPropertyChanged(nameof(ActorsList));
        OnPropertyChanged(nameof(TotalDurationDisplay));
        OnPropertyChanged(nameof(EventCountDisplay));
        OnPropertyChanged(nameof(IntervalCountDisplay));
        _filteredEventsView?.Refresh();
        TimelineRepaintRequested?.Invoke();
    }

    public static string FormatTimeUs(double timeUs)
    {
        if (timeUs < 1000)
            return $"{timeUs:F2} µs";
        if (timeUs < 1_000_000)
            return $"{timeUs / 1000.0:F3} ms";
        return $"{timeUs / 1_000_000.0:F4} s";
    }
}
