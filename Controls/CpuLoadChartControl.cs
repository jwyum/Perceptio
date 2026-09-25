using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Tracealyzer.Models;
using Tracealyzer.ViewModels;

namespace Tracealyzer.Controls;

public class CpuLoadChartControl : FrameworkElement
{
    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(22, 24, 27));
    private static readonly Brush GridBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
    private static readonly Pen GridPen = new(GridBrush, 1.0);
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(40, 43, 50)), 1.0);
    private static readonly Typeface DefaultTypeface = new("Segoe UI");

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MainViewModel), typeof(CpuLoadChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnViewModelChanged));

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (CpuLoadChartControl)d;
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

    public CpuLoadChartControl()
    {
        Height = 120;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 20 || height < 20 || ViewModel == null) return;

        var vm = ViewModel;
        var session = vm.Session;
        double viewStart = vm.ViewportStartUs;
        double viewEnd = vm.ViewportEndUs;
        double viewDuration = Math.Max(1.0, viewEnd - viewStart);

        // Background
        dc.DrawRectangle(BgBrush, BorderPen, new Rect(0, 0, width, height));

        // Draw Y-axis grid (0%, 25%, 50%, 75%, 100%)
        double[] levels = { 0, 25, 50, 75, 100 };
        foreach (var lvl in levels)
        {
            double y = height - (lvl / 100.0) * (height - 24) - 12;
            dc.DrawLine(GridPen, new Point(35, y), new Point(width, y));

            var text = new FormattedText(
                $"{lvl:F0}%",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                9,
                new SolidColorBrush(Color.FromRgb(120, 130, 145)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(text, new Point(32 - text.Width, y - text.Height / 2));
        }

        // Subdivide viewport into N time buckets (e.g. 50 buckets) to calculate CPU load slice
        int bucketCount = Math.Max(20, (int)(width / 15));
        double bucketDurationUs = viewDuration / bucketCount;
        double chartWidth = width - 40;

        var intervals = session.GetIntervalsInRange(viewStart, viewEnd);
        var visibleActors = session.Actors.Values.Where(a => a.IsVisible && a.Type != ActorType.Idle).ToList();

        // Accumulate runtime per bucket
        for (int b = 0; b < bucketCount; b++)
        {
            double bStart = viewStart + b * bucketDurationUs;
            double bEnd = bStart + bucketDurationUs;
            double x = 40 + (b / (double)bucketCount) * chartWidth;
            double barW = Math.Max(1.5, chartWidth / bucketCount - 1);

            double currentStackY = height - 12;

            foreach (var actor in visibleActors)
            {
                // Calculate actor execution time in this bucket
                double execTimeInBucket = 0;
                foreach (var iv in intervals)
                {
                    if (iv.Actor.Id != actor.Id) continue;
                    double overlapStart = Math.Max(bStart, iv.StartUs);
                    double overlapEnd = Math.Min(bEnd, iv.EndUs);
                    if (overlapEnd > overlapStart)
                    {
                        execTimeInBucket += (overlapEnd - overlapStart);
                    }
                }

                double cpuPercent = Math.Clamp((execTimeInBucket / bucketDurationUs) * 100.0, 0, 100);
                if (cpuPercent > 0.5)
                {
                    double segHeight = (cpuPercent / 100.0) * (height - 24);
                    var segRect = new Rect(x, currentStackY - segHeight, barW, segHeight);
                    dc.DrawRectangle(actor.Brush, null, segRect);
                    currentStackY -= segHeight;
                }
            }
        }

        // Title tag
        var titleText = new FormattedText(
            "CPU LOAD BREAKDOWN OVER TIME (TASK UTILIZATION %)",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            10,
            new SolidColorBrush(Color.FromRgb(160, 175, 195)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(titleText, new Point(42, 6));
    }
}
