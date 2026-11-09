using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TopoAlign.Views;

/// <summary>A small long-section plot of the profile: station along the bottom, elevation up the side.</summary>
internal sealed class ProfileGraph : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(ProfileGraphData), typeof(ProfileGraph),
        new FrameworkPropertyMetadata(ProfileGraphData.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(ProfileGraph),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public ProfileGraphData Data
    {
        get => (ProfileGraphData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Index into <see cref="ProfileGraphData.Stations"/> of the station to highlight, or -1.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private const double LeftMargin = 56.0;
    private const double RightMargin = 12.0;
    private const double TopMargin = 10.0;
    private const double BottomMargin = 30.0;

    private static readonly Brush Background_ = Freeze(new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)));
    private static readonly Brush GridBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xE2)));
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
    private static readonly Brush LineBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xB5)));
    private static readonly Brush FillBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x28, 0x1F, 0x6F, 0xB5)));
    private static readonly Brush OriginalBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8)));
    private static readonly Brush SelectedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x1E)));
    private static readonly Pen FramePen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0))), 1.0));
    private static readonly Pen GridPen = FreezePen(new Pen(GridBrush, 1.0));
    private static readonly Pen LinePen = FreezePen(new Pen(LineBrush, 2.0) { LineJoin = PenLineJoin.Round });
    private static readonly Pen OriginalPen = FreezePen(new Pen(OriginalBrush, 1.5) { DashStyle = DashStyles.Dash });
    private static readonly Pen MarkerPen = FreezePen(new Pen(Brushes.White, 1.0));

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Pen FreezePen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var size = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Background_, null, size);

        var plot = new Rect(LeftMargin, TopMargin, ActualWidth - LeftMargin - RightMargin, ActualHeight - TopMargin - BottomMargin);
        if (plot.Width < 20 || plot.Height < 20)
        {
            return;
        }

        var data = Data;
        var all = data.Line.Concat(data.Original).Concat(data.Stations).ToList();
        if (all.Count < 2)
        {
            DrawText(dc, "No profile", new Point(plot.Left + plot.Width / 2, plot.Top + plot.Height / 2), 12, TextBrush, center: true);
            return;
        }

        var minX = all.Min(p => p.X);
        var maxX = all.Max(p => p.X);
        var minY = all.Min(p => p.Y);
        var maxY = all.Max(p => p.Y);

        // leave some air above and below the profile; a flat profile still gets a visible range
        var padding = Math.Max((maxY - minY) * 0.12, 1e-6);
        if (maxY - minY < 1e-9)
        {
            padding = Math.Max(Math.Abs(maxY) * 0.01, 0.5);
        }

        minY -= padding;
        maxY += padding;
        if (maxX - minX < 1e-9)
        {
            maxX = minX + 1.0;
        }

        var yTicks = NiceTicks(minY, maxY, Math.Max(2, (int)(plot.Height / 38)));
        var xTicks = NiceTicks(minX, maxX, Math.Max(2, (int)(plot.Width / 90)));

        Point ToScreen(Point p) => new(
            plot.Left + (p.X - minX) / (maxX - minX) * plot.Width,
            plot.Bottom - (p.Y - minY) / (maxY - minY) * plot.Height);

        foreach (var y in yTicks.Where(t => t >= minY && t <= maxY))
        {
            var screen = ToScreen(new Point(minX, y)).Y;
            dc.DrawLine(GridPen, new Point(plot.Left, screen), new Point(plot.Right, screen));
            DrawText(dc, Format(y), new Point(plot.Left - 5, screen), 10, TextBrush, right: true);
        }

        foreach (var x in xTicks.Where(t => t >= minX && t <= maxX))
        {
            var screen = ToScreen(new Point(x, minY)).X;
            dc.DrawLine(GridPen, new Point(screen, plot.Top), new Point(screen, plot.Bottom));
            DrawText(dc, Format(x), new Point(screen, plot.Bottom + 4), 10, TextBrush, center: true, top: true);
        }

        dc.DrawRectangle(null, FramePen, plot);

        if (data.Unit.Length > 0)
        {
            DrawText(dc, $"Station / elevation ({data.Unit})", new Point(plot.Right, plot.Bottom + 15), 10, TextBrush, right: true, top: true);
        }

        dc.PushClip(new RectangleGeometry(plot));
        try
        {
            if (data.Original.Count > 1)
            {
                dc.DrawGeometry(null, OriginalPen, Polyline(data.Original.Select(ToScreen).ToList(), null));
            }

            if (data.Line.Count > 1)
            {
                var screenLine = data.Line.Select(ToScreen).ToList();
                dc.DrawGeometry(FillBrush, null, Polyline(screenLine, plot.Bottom));
                dc.DrawGeometry(null, LinePen, Polyline(screenLine, null));
            }
        }
        finally
        {
            dc.Pop();
        }

        for (var i = 0; i < data.Stations.Count; i++)
        {
            var selected = i == SelectedIndex;
            var centre = ToScreen(data.Stations[i]);
            if (selected)
            {
                dc.DrawLine(GridPen, new Point(centre.X, plot.Top), new Point(centre.X, plot.Bottom));
            }

            dc.DrawEllipse(selected ? SelectedBrush : LineBrush, MarkerPen, centre, selected ? 5.5 : 4.0, selected ? 5.5 : 4.0);
        }
    }

    /// <summary>A polyline through the points; with <paramref name="closeToY"/> it is closed down to that screen Y to be filled.</summary>
    private static System.Windows.Media.Geometry Polyline(IReadOnlyList<Point> points, double? closeToY)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], closeToY.HasValue, closeToY.HasValue);
            context.PolyLineTo(points.Skip(1).ToList(), true, true);
            if (closeToY.HasValue)
            {
                context.LineTo(new Point(points[points.Count - 1].X, closeToY.Value), false, false);
                context.LineTo(new Point(points[0].X, closeToY.Value), false, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private void DrawText(DrawingContext dc, string text, Point anchor, double size, Brush brush, bool center = false, bool right = false, bool top = false)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var x = center ? anchor.X - formatted.Width / 2 : right ? anchor.X - formatted.Width : anchor.X;
        var y = top ? anchor.Y : anchor.Y - formatted.Height / 2;
        dc.DrawText(formatted, new Point(x, y));
    }

    internal static string Format(double value)
    {
        return Math.Abs(value) >= 100 ? value.ToString("0", CultureInfo.CurrentCulture) : value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    /// <summary>Round-numbered tick values (1, 2 or 5 times a power of ten) covering the range with about <paramref name="target"/> ticks.</summary>
    internal static List<double> NiceTicks(double min, double max, int target)
    {
        var ticks = new List<double>();
        var range = max - min;
        if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range))
        {
            return ticks;
        }

        var rough = range / Math.Max(target, 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var fraction = rough / magnitude;
        var step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;

        var first = Math.Ceiling(min / step) * step;
        for (var value = first; value <= max + step * 1e-9 && ticks.Count < 50; value += step)
        {
            ticks.Add(Math.Round(value / step) * step);
        }

        return ticks;
    }
}