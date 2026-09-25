using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tracealyzer.Models;
using Tracealyzer.ViewModels;

namespace Tracealyzer.Controls;

public class TraceTimelineControl : FrameworkElement
{
    private const double HeaderHeight = 36.0;
    private const double LaneHeaderWidth = 140.0;
    private const double LaneHeight = 34.0;
    private const double LaneSpacing = 4.0;

    private Point? _panStartMousePoint;
    private double _panStartViewStart;
    private double _panStartViewEnd;
    private bool _isPanning;
    private Point _currentMousePos;

    // Cache pens and brushes
    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(24, 25, 28)); // #18191c
    private static readonly Brush LaneBgBrush1 = new SolidColorBrush(Color.FromRgb(32, 34, 38)); // #202226
    private static readonly Brush LaneBgBrush2 = new SolidColorBrush(Color.FromRgb(28, 30, 33)); // #1c1e21
    private static readonly Brush HeaderBgBrush = new SolidColorBrush(Color.FromRgb(36, 38, 43)); // #24262b
    private static readonly Brush GridLineBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly Pen GridPen = new(GridLineBrush, 1.0);
    private static readonly Pen SeparatorPen = new(new SolidColorBrush(Color.FromRgb(45, 48, 55)), 1.0);
    private static readonly Pen CursorPen = new(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.0);
    private static readonly Pen Marker1Pen = new(new SolidColorBrush(Color.FromRgb(0, 220, 255)), 1.5);
    private static readonly Pen Marker2Pen = new(new SolidColorBrush(Color.FromRgb(255, 200, 0)), 1.5);
    private static readonly Brush SelectionGlowBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
    private static readonly Pen SelectedIntervalPen = new(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2.0);

    private static readonly Typeface DefaultTypeface = new("Segoe UI");
    private static readonly Typeface BoldTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    static TraceTimelineControl()
    {
        ClipToBoundsProperty.OverrideMetadata(typeof(TraceTimelineControl), new FrameworkPropertyMetadata(true));
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MainViewModel), typeof(TraceTimelineControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewModelChanged));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TraceTimelineControl)d;
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.TimelineRangeChanged -= ctrl.InvalidateVisual;
            oldVm.TimelineRepaintRequested -= ctrl.InvalidateVisual;
        }
        if (e.NewValue is MainViewModel newVm)
        {
            newVm.TimelineRangeChanged += ctrl.InvalidateVisual;
            newVm.TimelineRepaintRequested += ctrl.InvalidateVisual;
        }
        ctrl.InvalidateVisual();
    }

    public TraceTimelineControl()
    {
        Focusable = true;
        Cursor = Cursors.Cross;

        Loaded += (_, _) =>
        {
            if (ViewModel != null)
            {
                ViewModel.TimelineRangeChanged += InvalidateVisual;
                ViewModel.TimelineRepaintRequested += InvalidateVisual;
            }
        };

        Unloaded += (_, _) =>
        {
            if (ViewModel != null)
            {
                ViewModel.TimelineRangeChanged -= InvalidateVisual;
                ViewModel.TimelineRepaintRequested -= InvalidateVisual;
            }
        };
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 10 || height < 10 || ViewModel == null) return;

        var vm = ViewModel;
        var session = vm.Session;
        double viewStart = vm.ViewportStartUs;
        double viewEnd = vm.ViewportEndUs;
        double viewDuration = Math.Max(1.0, viewEnd - viewStart);
        double plotWidth = Math.Max(1.0, width - LaneHeaderWidth);

        // 1. Background
        dc.DrawRectangle(BgBrush, null, new Rect(0, 0, width, height));

        // Get visible actors
        var visibleActors = session.Actors.Values.Where(a => a.IsVisible).OrderByDescending(a => a.Priority).ToList();
        double totalLanesHeight = visibleActors.Count * (LaneHeight + LaneSpacing) + HeaderHeight;

        // 2. Draw Lanes Background & Left Headers
        double currentY = HeaderHeight;
        for (int i = 0; i < visibleActors.Count; i++)
        {
            var actor = visibleActors[i];
            var laneRect = new Rect(LaneHeaderWidth, currentY, plotWidth, LaneHeight);
            var headerRect = new Rect(0, currentY, LaneHeaderWidth, LaneHeight);

            // Alternating lane background
            dc.DrawRectangle(i % 2 == 0 ? LaneBgBrush1 : LaneBgBrush2, null, laneRect);

            // Left header
            dc.DrawRectangle(HeaderBgBrush, null, headerRect);
            dc.DrawLine(SeparatorPen, new Point(0, currentY + LaneHeight), new Point(width, currentY + LaneHeight));

            // Actor color pill
            var pillRect = new Rect(8, currentY + (LaneHeight - 14) / 2, 6, 14);
            dc.DrawRoundedRectangle(actor.Brush, null, pillRect, 3, 3);

            // Actor Name
            var nameText = new FormattedText(
                actor.Name,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BoldTypeface,
                12,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = 85,
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };
            dc.DrawText(nameText, new Point(20, currentY + (LaneHeight - nameText.Height) / 2));

            // Priority badge
            string prioStr = actor.Type == ActorType.Isr ? $"ISR" : $"P{actor.Priority}";
            var prioText = new FormattedText(
                prioStr,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                10,
                new SolidColorBrush(Color.FromRgb(160, 165, 175)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(prioText, new Point(LaneHeaderWidth - prioText.Width - 8, currentY + (LaneHeight - prioText.Height) / 2));

            currentY += LaneHeight + LaneSpacing;
        }

        // Header separator line
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(60, 65, 75)), 1.5), new Point(LaneHeaderWidth, 0), new Point(LaneHeaderWidth, height));

        // 3. Time Grid Lines & Adaptive Ticks
        DrawTimeGrid(dc, viewStart, viewEnd, viewDuration, plotWidth, height);

        // 4. Execution Intervals
        var intervals = session.GetIntervalsInRange(viewStart, viewEnd);
        var actorYMap = new Dictionary<uint, double>();
        currentY = HeaderHeight;
        foreach (var actor in visibleActors)
        {
            actorYMap[actor.Id] = currentY;
            currentY += LaneHeight + LaneSpacing;
        }

        foreach (var interval in intervals)
        {
            if (!actorYMap.TryGetValue(interval.Actor.Id, out double laneY))
                continue;

            double x1 = LaneHeaderWidth + ((interval.StartUs - viewStart) / viewDuration) * plotWidth;
            double x2 = LaneHeaderWidth + ((interval.EndUs - viewStart) / viewDuration) * plotWidth;

            // Clamp or discard out-of-screen
            if (x2 < LaneHeaderWidth || x1 > width) continue;

            double blockWidth = Math.Max(2.0, x2 - x1);
            double blockHeight = LaneHeight - 6.0;
            var blockRect = new Rect(x1, laneY + 3.0, blockWidth, blockHeight);

            bool isSelected = vm.SelectedInterval == interval;

            // Draw interval block
            dc.DrawRoundedRectangle(interval.Actor.Brush, isSelected ? SelectedIntervalPen : null, blockRect, 3, 3);

            if (isSelected)
            {
                dc.DrawRoundedRectangle(SelectionGlowBrush, null, new Rect(x1 - 1, laneY + 2, blockWidth + 2, blockHeight + 2), 4, 4);
            }

            // If preemption occurred, draw subtle preemption notch
            if (interval.IsPreempted)
            {
                var preemptionPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.5);
                dc.DrawLine(preemptionPen, new Point(x2 - 1, laneY + 3), new Point(x2 - 1, laneY + 3 + blockHeight));
            }

            // Draw duration text if block is wide enough
            if (blockWidth > 45)
            {
                string durText = interval.DurationUs < 1000 ? $"{interval.DurationUs:F0}µs" : $"{interval.DurationUs / 1000.0:F1}ms";
                var text = new FormattedText(
                    durText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    DefaultTypeface,
                    10,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip)
                {
                    MaxTextWidth = blockWidth - 4,
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis
                };
                dc.DrawText(text, new Point(x1 + 4, laneY + (LaneHeight - text.Height) / 2));
            }
        }

        // 5. Draw Time Ruler Header (Top)
        DrawRulerHeader(dc, viewStart, viewEnd, viewDuration, plotWidth, width);

        // 6. Draw Measurement Markers (T1, T2) & Delta
        DrawMeasurementMarkers(dc, vm, viewStart, viewDuration, plotWidth, height);

        // 7. Mouse Hover Hairline Cursor
        if (_currentMousePos.X >= LaneHeaderWidth && _currentMousePos.X <= width)
        {
            double hoverTime = viewStart + ((_currentMousePos.X - LaneHeaderWidth) / plotWidth) * viewDuration;
            dc.DrawLine(CursorPen, new Point(_currentMousePos.X, HeaderHeight), new Point(_currentMousePos.X, height));

            // Cursor tooltip badge
            string cursorStr = MainViewModel.FormatTimeUs(hoverTime);
            var cursorText = new FormattedText(
                cursorStr,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                11,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double badgeW = cursorText.Width + 10;
            double badgeH = cursorText.Height + 4;
            double badgeX = Math.Clamp(_currentMousePos.X - badgeW / 2, LaneHeaderWidth + 2, width - badgeW - 2);
            var badgeRect = new Rect(badgeX, HeaderHeight + 2, badgeW, badgeH);

            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 20, 22, 28)), null, badgeRect, 3, 3);
            dc.DrawText(cursorText, new Point(badgeX + 5, HeaderHeight + 4));
        }
    }

    private void DrawRulerHeader(DrawingContext dc, double viewStart, double viewEnd, double viewDuration, double plotWidth, double totalWidth)
    {
        var headerRect = new Rect(0, 0, totalWidth, HeaderHeight);
        dc.DrawRectangle(HeaderBgBrush, null, headerRect);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(55, 60, 70)), 1.5), new Point(0, HeaderHeight), new Point(totalWidth, HeaderHeight));

        // Corner box
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(42, 45, 52)), null, new Rect(0, 0, LaneHeaderWidth, HeaderHeight));
        var cornerText = new FormattedText(
            "ACTORS / TASKS",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            BoldTypeface,
            11,
            new SolidColorBrush(Color.FromRgb(150, 160, 175)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(cornerText, new Point(12, (HeaderHeight - cornerText.Height) / 2));
    }

    private void DrawTimeGrid(DrawingContext dc, double viewStart, double viewEnd, double viewDuration, double plotWidth, double height)
    {
        double stepUs = CalculateAdaptiveTimeStep(viewDuration);
        double firstTick = Math.Floor(viewStart / stepUs) * stepUs;

        for (double t = firstTick; t <= viewEnd + stepUs; t += stepUs)
        {
            if (t < viewStart) continue;

            double x = LaneHeaderWidth + ((t - viewStart) / viewDuration) * plotWidth;
            if (x < LaneHeaderWidth || x > LaneHeaderWidth + plotWidth) continue;

            // Vertical grid line
            dc.DrawLine(GridPen, new Point(x, HeaderHeight), new Point(x, height));

            // Tick mark on header
            dc.DrawLine(new Pen(Brushes.Gray, 1), new Point(x, HeaderHeight - 8), new Point(x, HeaderHeight));

            // Time label on header
            string label = MainViewModel.FormatTimeUs(t);
            var labelText = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                10,
                new SolidColorBrush(Color.FromRgb(175, 185, 200)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(labelText, new Point(x + 4, HeaderHeight - labelText.Height - 3));
        }
    }

    private void DrawMeasurementMarkers(DrawingContext dc, MainViewModel vm, double viewStart, double viewDuration, double plotWidth, double height)
    {
        var m = vm.Measurement;
        double? x1 = null;
        double? x2 = null;

        if (m.Marker1Us.HasValue)
        {
            double m1 = m.Marker1Us.Value;
            double posX = LaneHeaderWidth + ((m1 - viewStart) / viewDuration) * plotWidth;
            x1 = posX;

            if (posX >= LaneHeaderWidth && posX <= ActualWidth)
            {
                dc.DrawLine(Marker1Pen, new Point(posX, HeaderHeight), new Point(posX, height));

                // Flag tag T1
                var t1Text = new FormattedText("T1: " + MainViewModel.FormatTimeUs(m1), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BoldTypeface, 10, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                var tagRect = new Rect(posX, HeaderHeight, t1Text.Width + 6, t1Text.Height + 2);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0, 220, 255)), null, tagRect);
                dc.DrawText(t1Text, new Point(posX + 3, HeaderHeight + 1));
            }
        }

        if (m.Marker2Us.HasValue)
        {
            double m2 = m.Marker2Us.Value;
            double posX = LaneHeaderWidth + ((m2 - viewStart) / viewDuration) * plotWidth;
            x2 = posX;

            if (posX >= LaneHeaderWidth && posX <= ActualWidth)
            {
                dc.DrawLine(Marker2Pen, new Point(posX, HeaderHeight), new Point(posX, height));

                // Flag tag T2
                var t2Text = new FormattedText("T2: " + MainViewModel.FormatTimeUs(m2), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BoldTypeface, 10, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                var tagRect = new Rect(posX, HeaderHeight + 16, t2Text.Width + 6, t2Text.Height + 2);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255, 200, 0)), null, tagRect);
                dc.DrawText(t2Text, new Point(posX + 3, HeaderHeight + 17));
            }
        }

        // Draw measurement span and delta readout
        if (x1.HasValue && x2.HasValue)
        {
            double left = Math.Min(x1.Value, x2.Value);
            double right = Math.Max(x1.Value, x2.Value);
            double spanWidth = Math.Max(2.0, right - left);

            // Shaded measurement region
            var shadeRect = new Rect(left, HeaderHeight, spanWidth, height - HeaderHeight);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(25, 0, 220, 255)), null, shadeRect);

            // Delta banner
            string bannerStr = $"ΔT: {m.FormattedDelta}   Freq: {m.FormattedFrequency}";
            var bannerText = new FormattedText(
                bannerStr,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BoldTypeface,
                11,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double bW = bannerText.Width + 14;
            double bH = bannerText.Height + 6;
            double bX = Math.Clamp((left + right - bW) / 2.0, LaneHeaderWidth + 5, ActualWidth - bW - 5);
            var bRect = new Rect(bX, HeaderHeight + 35, bW, bH);

            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)), new Pen(new SolidColorBrush(Color.FromRgb(0, 180, 216)), 1), bRect, 4, 4);
            dc.DrawText(bannerText, new Point(bX + 7, HeaderHeight + 38));
        }
    }

    private static double CalculateAdaptiveTimeStep(double durationUs)
    {
        // Target ~8 to 15 divisions across width
        double rawStep = durationUs / 10.0;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double ratio = rawStep / magnitude;

        if (ratio < 1.5) return 1.0 * magnitude;
        if (ratio < 3.0) return 2.0 * magnitude;
        if (ratio < 7.0) return 5.0 * magnitude;
        return 10.0 * magnitude;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _currentMousePos = e.GetPosition(this);

        if (_isPanning && _panStartMousePoint.HasValue && ViewModel != null)
        {
            double plotWidth = Math.Max(1.0, ActualWidth - LaneHeaderWidth);
            double deltaPixels = _currentMousePos.X - _panStartMousePoint.Value.X;
            double initialDuration = _panStartViewEnd - _panStartViewStart;
            double deltaUs = (deltaPixels / plotWidth) * initialDuration;

            double newStart = _panStartViewStart - deltaUs;
            double newEnd = _panStartViewEnd - deltaUs;

            if (newStart < 0)
            {
                newStart = 0;
                newEnd = initialDuration;
            }

            ViewModel.ViewportStartUs = newStart;
            ViewModel.ViewportEndUs = newEnd;
            ViewModel.FollowLive = false;
        }

        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (ViewModel == null) return;

        double plotWidth = Math.Max(1.0, ActualWidth - LaneHeaderWidth);
        var mouseX = e.GetPosition(this).X;

        double centerUs;
        if (mouseX >= LaneHeaderWidth && mouseX <= ActualWidth)
        {
            centerUs = ViewModel.ViewportStartUs + ((mouseX - LaneHeaderWidth) / plotWidth) * ViewModel.ViewportDurationUs;
        }
        else
        {
            centerUs = ViewModel.ViewportStartUs + ViewModel.ViewportDurationUs / 2.0;
        }

        double factor = e.Delta > 0 ? 0.75 : 1.33;
        ViewModel.Zoom(factor, centerUs);
        ViewModel.FollowLive = false;
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        var pos = e.GetPosition(this);
        if (ViewModel == null) return;

        double plotWidth = Math.Max(1.0, ActualWidth - LaneHeaderWidth);
        double clickTimeUs = ViewModel.ViewportStartUs + Math.Clamp((pos.X - LaneHeaderWidth) / plotWidth, 0.0, 1.0) * ViewModel.ViewportDurationUs;

        if (e.ChangedButton == MouseButton.Left)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                // Shift + Click sets Marker 2
                ViewModel.SetMeasurementMarker2(clickTimeUs);
                return;
            }

            // Check if user clicked an interval
            var hitInterval = FindIntervalAtPoint(pos);
            if (hitInterval != null)
            {
                ViewModel.SelectedInterval = hitInterval;
                if (hitInterval.StartEvent != null)
                {
                    ViewModel.SelectedEvent = hitInterval.StartEvent;
                }
            }
            else if (pos.Y < HeaderHeight)
            {
                // Clicked on ruler sets Marker 1
                ViewModel.SetMeasurementMarker1(clickTimeUs);
            }
            else
            {
                // Start pan
                _isPanning = true;
                _panStartMousePoint = pos;
                _panStartViewStart = ViewModel.ViewportStartUs;
                _panStartViewEnd = ViewModel.ViewportEndUs;
                CaptureMouse();
            }
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            // Right-click sets Marker 2
            ViewModel.SetMeasurementMarker2(clickTimeUs);
        }
        else if (e.ChangedButton == MouseButton.Middle)
        {
            // Middle button starts pan
            _isPanning = true;
            _panStartMousePoint = pos;
            _panStartViewStart = ViewModel.ViewportStartUs;
            _panStartViewEnd = ViewModel.ViewportEndUs;
            CaptureMouse();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isPanning)
        {
            _isPanning = false;
            _panStartMousePoint = null;
            ReleaseMouseCapture();
        }
    }

    private ExecutionInterval? FindIntervalAtPoint(Point pos)
    {
        if (ViewModel == null || pos.X < LaneHeaderWidth || pos.Y < HeaderHeight)
            return null;

        var session = ViewModel.Session;
        var visibleActors = session.Actors.Values.Where(a => a.IsVisible).OrderByDescending(a => a.Priority).ToList();
        double currentY = HeaderHeight;
        TraceActor? clickedActor = null;

        foreach (var actor in visibleActors)
        {
            if (pos.Y >= currentY && pos.Y <= currentY + LaneHeight)
            {
                clickedActor = actor;
                break;
            }
            currentY += LaneHeight + LaneSpacing;
        }

        if (clickedActor == null) return null;

        double plotWidth = Math.Max(1.0, ActualWidth - LaneHeaderWidth);
        double clickTime = ViewModel.ViewportStartUs + ((pos.X - LaneHeaderWidth) / plotWidth) * ViewModel.ViewportDurationUs;

        var candidates = session.GetIntervalsInRange(clickTime - 1, clickTime + 1);
        return candidates.FirstOrDefault(i => i.Actor.Id == clickedActor.Id && clickTime >= i.StartUs && clickTime <= i.EndUs);
    }
}
