using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tracealyzer.ViewModels;

namespace Tracealyzer.Controls;

public class TraceMinimapControl : FrameworkElement
{
    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(20, 21, 24));
    private static readonly Brush ViewportBrush = new SolidColorBrush(Color.FromArgb(80, 0, 180, 216));
    private static readonly Pen ViewportPen = new(new SolidColorBrush(Color.FromRgb(0, 180, 216)), 1.5);
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(40, 43, 50)), 1.0);

    private bool _isDraggingViewport;
    private double _dragOffsetUs;

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MainViewModel), typeof(TraceMinimapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewModelChanged));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TraceMinimapControl)d;
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

    public TraceMinimapControl()
    {
        Cursor = Cursors.Hand;
        Height = 28;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 10 || height < 5 || ViewModel == null) return;

        var vm = ViewModel;
        var session = vm.Session;
        double totalDuration = Math.Max(1.0, session.DurationUs);

        // Background
        dc.DrawRectangle(BgBrush, BorderPen, new Rect(0, 0, width, height));

        // Draw mini execution streaks
        var intervals = session.Intervals;
        if (intervals.Count > 0)
        {
            // Sample intervals to avoid drawing 100k lines in 30px height
            int step = Math.Max(1, intervals.Count / 1500);
            for (int i = 0; i < intervals.Count; i += step)
            {
                var interval = intervals[i];
                if (!interval.Actor.IsVisible) continue;

                double x1 = (interval.StartUs / totalDuration) * width;
                double x2 = (interval.EndUs / totalDuration) * width;
                double w = Math.Max(1.5, x2 - x1);

                dc.DrawRectangle(interval.Actor.Brush, null, new Rect(x1, 2, w, height - 4));
            }
        }

        // Draw Viewport window rectangle
        double vpX1 = Math.Clamp((vm.ViewportStartUs / totalDuration) * width, 0, width);
        double vpX2 = Math.Clamp((vm.ViewportEndUs / totalDuration) * width, 0, width);
        double vpWidth = Math.Max(4.0, vpX2 - vpX1);

        var vpRect = new Rect(vpX1, 1, vpWidth, height - 2);
        dc.DrawRoundedRectangle(ViewportBrush, ViewportPen, vpRect, 2, 2);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (ViewModel == null || ActualWidth < 10) return;

        var pos = e.GetPosition(this);
        double totalDuration = Math.Max(1.0, ViewModel.Session.DurationUs);
        double clickedTimeUs = (pos.X / ActualWidth) * totalDuration;

        double curWindow = ViewModel.ViewportDurationUs;
        double newStart = Math.Clamp(clickedTimeUs - curWindow / 2.0, 0, Math.Max(0, totalDuration - curWindow));

        ViewModel.ViewportStartUs = newStart;
        ViewModel.ViewportEndUs = newStart + curWindow;
        ViewModel.FollowLive = false;

        _isDraggingViewport = true;
        _dragOffsetUs = curWindow / 2.0;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDraggingViewport && ViewModel != null && ActualWidth > 10)
        {
            var pos = e.GetPosition(this);
            double totalDuration = Math.Max(1.0, ViewModel.Session.DurationUs);
            double clickedTimeUs = (pos.X / ActualWidth) * totalDuration;

            double curWindow = ViewModel.ViewportDurationUs;
            double newStart = Math.Clamp(clickedTimeUs - _dragOffsetUs, 0, Math.Max(0, totalDuration - curWindow));

            ViewModel.ViewportStartUs = newStart;
            ViewModel.ViewportEndUs = newStart + curWindow;
            ViewModel.FollowLive = false;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDraggingViewport)
        {
            _isDraggingViewport = false;
            ReleaseMouseCapture();
        }
    }
}
