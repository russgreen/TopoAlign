using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TopoAlign.Views;

/// <summary>What <see cref="PlanGraph"/> draws, in display units: the edited alignment, the original, and the points between.</summary>
internal sealed class PlanGraphData
{
    public static readonly PlanGraphData Empty = new(Array.Empty<Point>(), Array.Empty<Point>(), Array.Empty<Point>(), Array.Empty<Point>(), string.Empty);

    public PlanGraphData(IReadOnlyList<Point> line, IReadOnlyList<Point> original, IReadOnlyList<Point> tangents, IReadOnlyList<Point> points, string unit)
    {
        Line = line;
        Original = original;
        Tangents = tangents;
        Points = points;
        Unit = unit;
    }

    /// <summary>The edited alignment as a polyline (X = easting, Y = northing). Empty while the points can't be laid out.</summary>
    public IReadOnlyList<Point> Line { get; }

    /// <summary>The alignment as it is in the model, drawn faintly behind the edited one.</summary>
    public IReadOnlyList<Point> Original { get; }

    /// <summary>The straight lines through the points of intersection, drawn dotted so the curves can be seen against them.</summary>
    public IReadOnlyList<Point> Tangents { get; }

    /// <summary>One marker per point of intersection.</summary>
    public IReadOnlyList<Point> Points { get; }

    public string Unit { get; }
}

/// <summary>A small plan view of the alignment: equal scale in both directions, with the points of intersection marked and numbered.</summary>
internal sealed class PlanGraph : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(PlanGraphData), typeof(PlanGraph),
        new FrameworkPropertyMetadata(PlanGraphData.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(PlanGraph),
        new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public PlanGraphData Data
    {
        get => (PlanGraphData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Index into <see cref="PlanGraphData.Points"/> of the point to highlight, or -1.</summary>
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
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
    private static readonly Brush LineBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xB5)));
    private static readonly Brush SelectedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x53, 0x1E)));
    private static readonly Pen FramePen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0))), 1.0));
    private static readonly Pen GridPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xE2))), 1.0));
    private static readonly Pen LinePen = FreezePen(new Pen(LineBrush, 2.0) { LineJoin = PenLineJoin.Round });
    private static readonly Pen OriginalPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8))), 1.5) { DashStyle = DashStyles.Dash });
    private static readonly Pen TangentPen = FreezePen(new Pen(Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0xA6, 0xC1))), 1.0) { DashStyle = DashStyles.Dot });
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
        var all = data.Line.Concat(data.Original).Concat(data.Tangents).Concat(data.Points).ToList();
        if (all.Count < 2)
        {
            DrawText(dc, "No alignment", new Point(plot.Left + plot.Width / 2, plot.Top + plot.Height / 2), 12, TextBrush, center: true);
            return;
        }

        var minX = all.Min(p => p.X);
        var maxX = all.Max(p => p.X);
        var minY = all.Min(p => p.Y);
        var maxY = all.Max(p => p.Y);

        // equal scale both ways, centred in the plot, with some air around the alignment
        var spanX = Math.Max(maxX - minX, 1e-3);
        var spanY = Math.Max(maxY - minY, 1e-3);
        var scale = Math.Min(plot.Width / (spanX * 1.12), plot.Height / (spanY * 1.12));
        var centreX = (minX + maxX) / 2.0;
        var centreY = (minY + maxY) / 2.0;

        Point ToScreen(Point p) => new(
            plot.Left + plot.Width / 2 + (p.X - centreX) * scale,
            plot.Top + plot.Height / 2 - (p.Y - centreY) * scale);

        var visibleMinX = centreX - plot.Width / 2 / scale;
        var visibleMaxX = centreX + plot.Width / 2 / scale;
        var visibleMinY = centreY - plot.Height / 2 / scale;
        var visibleMaxY = centreY + plot.Height / 2 / scale;

        foreach (var y in ProfileGraph.NiceTicks(visibleMinY, visibleMaxY, Math.Max(2, (int)(plot.Height / 38))))
        {
            var screen = ToScreen(new Point(centreX, y)).Y;
            dc.DrawLine(GridPen, new Point(plot.Left, screen), new Point(plot.Right, screen));
            DrawText(dc, ProfileGraph.Format(y), new Point(plot.Left - 5, screen), 10, TextBrush, right: true);
        }

        foreach (var x in ProfileGraph.NiceTicks(visibleMinX, visibleMaxX, Math.Max(2, (int)(plot.Width / 90))))
        {
            var screen = ToScreen(new Point(x, centreY)).X;
            dc.DrawLine(GridPen, new Point(screen, plot.Top), new Point(screen, plot.Bottom));
            DrawText(dc, ProfileGraph.Format(x), new Point(screen, plot.Bottom + 4), 10, TextBrush, center: true, top: true);
        }

        dc.DrawRectangle(null, FramePen, plot);

        if (data.Unit.Length > 0)
        {
            DrawText(dc, $"Plan, X / Y ({data.Unit})", new Point(plot.Right, plot.Bottom + 15), 10, TextBrush, right: true, top: true);
        }

        dc.PushClip(new RectangleGeometry(plot));
        try
        {
            if (data.Original.Count > 1)
            {
                dc.DrawGeometry(null, OriginalPen, Polyline(data.Original.Select(ToScreen).ToList()));
            }

            if (data.Tangents.Count > 1)
            {
                dc.DrawGeometry(null, TangentPen, Polyline(data.Tangents.Select(ToScreen).ToList()));
            }

            if (data.Line.Count > 1)
            {
                dc.DrawGeometry(null, LinePen, Polyline(data.Line.Select(ToScreen).ToList()));
            }

            for (var i = 0; i < data.Points.Count; i++)
            {
                var selected = i == SelectedIndex;
                var centre = ToScreen(data.Points[i]);
                var radius = selected ? 5.5 : 4.0;
                dc.DrawEllipse(selected ? SelectedBrush : LineBrush, MarkerPen, centre, radius, radius);
                DrawText(dc, (i + 1).ToString(CultureInfo.CurrentCulture), new Point(centre.X + radius + 2, centre.Y - radius - 5),
                    10, selected ? SelectedBrush : TextBrush);
            }
        }
        finally
        {
            dc.Pop();
        }
    }

    private static System.Windows.Media.Geometry Polyline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo(points.Skip(1).ToList(), true, true);
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
}
