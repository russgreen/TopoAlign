
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using TopoAlign.Sweep;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;

/// <summary>
/// Interaction logic for AssemblyEditorView.xaml. The grid is bound; the picture of the assembly is drawn here from
/// the outlines <see cref="AssemblyPreview"/> works out.
/// </summary>
public partial class AssemblyEditorView : Window
{
    private const double Pad = 34;

    private static readonly Color[] palette =
    {
        Color.FromRgb(0x1F, 0x77, 0xB4), Color.FromRgb(0xD6, 0x27, 0x28), Color.FromRgb(0x2C, 0xA0, 0x2C),
        Color.FromRgb(0xFF, 0x7F, 0x0E), Color.FromRgb(0x94, 0x67, 0xBD), Color.FromRgb(0x8C, 0x56, 0x4B),
        Color.FromRgb(0xE3, 0x77, 0xC2), Color.FromRgb(0x17, 0xBE, 0xCF)
    };

    private readonly AssemblyEditorViewModel _viewModel;

    internal AssemblyEditorView(AssemblyEditorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => Close();
        viewModel.PreviewChanged += (s, e) => Draw();
        Loaded += (s, e) => Draw();
    }

    /// <summary>A cell that has just gone into edit mode takes the keyboard straight away, with its text selected.</summary>
    private void NumericEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();

    private void Draw()
    {
        var canvas = PreviewCanvas;
        canvas.Children.Clear();

        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width < 2 * Pad + 10 || height < 2 * Pad + 10)
        {
            return;
        }

        var preview = _viewModel.Preview;
        if (!preview.HasOutlines)
        {
            AddText(canvas, "Choose a profile for a part to see the assembly.", 12, 12, Brushes.Gray);
            return;
        }

        // the centreline and the finished level are always in view, so offsets read against them
        var minU = Math.Min(preview.MinU, 0.0);
        var maxU = Math.Max(preview.MaxU, 0.0);
        var minV = Math.Min(preview.MinV, 0.0);
        var maxV = Math.Max(preview.MaxV, 0.0);
        var spanU = Math.Max(maxU - minU, 0.1);
        var spanV = Math.Max(maxV - minV, 0.1);

        var scale = Math.Min((width - 2 * Pad) / spanU, (height - 2 * Pad) / spanV);
        var left = (width - spanU * scale) / 2.0;
        var bottom = height - (height - spanV * scale) / 2.0;

        Point Map(double u, double v) => new(left + (u - minU) * scale, bottom - (v - minV) * scale);

        // finished level
        var origin = Map(0.0, 0.0);
        canvas.Children.Add(new Line
        {
            X1 = 0, X2 = width, Y1 = origin.Y, Y2 = origin.Y,
            Stroke = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)), StrokeThickness = 1
        });

        // centreline
        canvas.Children.Add(new Line
        {
            X1 = origin.X, X2 = origin.X, Y1 = 0, Y2 = height,
            Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 3 }
        });
        AddText(canvas, "◄ left", 6, 4, Brushes.Gray);
        AddText(canvas, "right ►", width - 52, 4, Brushes.Gray);
        AddText(canvas, "alignment", Math.Min(origin.X + 4, width - 60), height - 18, Brushes.Gray);

        for (var i = 0; i < preview.Outlines.Count; i++)
        {
            var outline = preview.Outlines[i];
            var colour = palette[outline.Index % palette.Length];
            var selected = outline.Index == _viewModel.SelectedIndex;

            var polygon = new Polygon
            {
                Stroke = new SolidColorBrush(colour),
                StrokeThickness = selected ? 2.5 : 1.25,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)110 : (byte)55, colour.R, colour.G, colour.B))
            };

            double top = double.MaxValue, minX = double.MaxValue;
            foreach (var point in outline.Points)
            {
                var p = Map(point.X, point.Y);
                polygon.Points.Add(p);
                top = Math.Min(top, p.Y);
                minX = Math.Min(minX, p.X);
            }

            canvas.Children.Add(polygon);
            AddText(canvas, outline.Name, Math.Max(2, Math.Min(minX, width - 70)), Math.Max(18, top - 16), new SolidColorBrush(colour));
        }

        // the cut and fill slopes running out from the edge of the assembly; the canvas clips them where they leave the picture
        var reach = (width + height) / scale;
        foreach (var slope in preview.Slopes)
        {
            var colour = slope.IsCut ? Color.FromRgb(0xB0, 0x5A, 0x00) : Color.FromRgb(0x2E, 0x7D, 0x32);
            var brush = new SolidColorBrush(colour);
            var start = Map(slope.U, slope.V);
            var endU = slope.U + slope.Direction * reach;
            var end = Map(endU, slope.HeightAt(endU));
            canvas.Children.Add(new Line
            {
                X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y,
                Stroke = brush, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 }
            });

            var labelU = slope.U + slope.Direction * 60.0 / scale;
            var labelAt = Map(labelU, slope.HeightAt(labelU));
            AddText(canvas, slope.Label,
                Math.Max(2, Math.Min(labelAt.X - (slope.Direction < 0 ? 44 : 0), width - 70)),
                Math.Max(2, Math.Min(labelAt.Y + (slope.IsCut ? -16 : 2), height - 18)),
                brush);
        }

        // the extent of the assembly across the road
        var unit = _viewModel.SelectedDisplayUnit;
        string Label(double feet) => $"{Autodesk.Revit.DB.UnitUtils.ConvertFromInternalUnits(feet, unit.DisplayUnit):0.###}";
        AddText(canvas, Label(minU), 4, height - 18, Brushes.Gray);
        AddText(canvas, $"{Label(maxU)} {unit.Label}", Math.Max(4, width - 80), height - 18, Brushes.Gray);
    }

    private static void AddText(Canvas canvas, string text, double x, double y, Brush brush)
    {
        var block = new TextBlock { Text = text, Foreground = brush, FontSize = 11 };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        canvas.Children.Add(block);
    }
}
