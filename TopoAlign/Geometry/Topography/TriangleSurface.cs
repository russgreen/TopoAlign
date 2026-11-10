namespace TopoAlign.Geometry.Topography;

/// <summary>
/// A set of triangles with a uniform XY grid index, answering "what is the surface elevation at (x, y)?".
/// Used to read ground elevations off a topography mesh without needing a 3D view or ray casting.
/// </summary>
internal sealed class TriangleSurface
{
    private const double Epsilon = 1e-9;
    private const int MaxCellsPerAxis = 512;

    private readonly List<double[]> _triangles = new();
    private List<int>[]? _cells;
    private int _columns;
    private int _rows;
    private double _minX;
    private double _minY;
    private double _cellWidth;
    private double _cellHeight;

    public int TriangleCount => _triangles.Count;

    public void AddTriangle(double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2)
    {
        _triangles.Add(new[] { x0, y0, z0, x1, y1, z1, x2, y2, z2 });
        _cells = null;
    }

    /// <summary>
    /// Elevation of the highest triangle covering (x, y), or false if the point is outside the surface.
    /// </summary>
    public bool TryGetElevation(double x, double y, out double elevation)
    {
        elevation = 0.0;
        if (_triangles.Count == 0)
        {
            return false;
        }

        var cells = _cells ?? BuildIndex();

        var column = (int)Math.Floor((x - _minX) / _cellWidth);
        var row = (int)Math.Floor((y - _minY) / _cellHeight);

        // points exactly on the max edge fall one cell past the end
        column = Math.Min(column, _columns - 1);
        row = Math.Min(row, _rows - 1);
        if (column < 0 || row < 0)
        {
            return false;
        }

        var found = false;
        var best = double.MinValue;
        foreach (var index in cells[row * _columns + column])
        {
            if (TryInterpolate(_triangles[index], x, y, out var z) && z > best)
            {
                best = z;
                found = true;
            }
        }

        if (found)
        {
            elevation = best;
        }

        return found;
    }

    private List<int>[] BuildIndex()
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var t in _triangles)
        {
            minX = Math.Min(minX, Math.Min(t[0], Math.Min(t[3], t[6])));
            maxX = Math.Max(maxX, Math.Max(t[0], Math.Max(t[3], t[6])));
            minY = Math.Min(minY, Math.Min(t[1], Math.Min(t[4], t[7])));
            maxY = Math.Max(maxY, Math.Max(t[1], Math.Max(t[4], t[7])));
        }

        var perAxis = (int)Math.Min(MaxCellsPerAxis, Math.Max(1, Math.Ceiling(Math.Sqrt(_triangles.Count))));
        _columns = perAxis;
        _rows = perAxis;
        _minX = minX;
        _minY = minY;
        _cellWidth = Math.Max((maxX - minX) / _columns, Epsilon);
        _cellHeight = Math.Max((maxY - minY) / _rows, Epsilon);

        var cells = new List<int>[_columns * _rows];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = new List<int>();
        }

        for (var index = 0; index < _triangles.Count; index++)
        {
            var t = _triangles[index];
            var c0 = ClampColumn(Math.Min(t[0], Math.Min(t[3], t[6])));
            var c1 = ClampColumn(Math.Max(t[0], Math.Max(t[3], t[6])));
            var r0 = ClampRow(Math.Min(t[1], Math.Min(t[4], t[7])));
            var r1 = ClampRow(Math.Max(t[1], Math.Max(t[4], t[7])));

            for (var r = r0; r <= r1; r++)
            {
                for (var c = c0; c <= c1; c++)
                {
                    cells[r * _columns + c].Add(index);
                }
            }
        }

        _cells = cells;
        return cells;
    }

    private int ClampColumn(double x) => Math.Max(0, Math.Min(_columns - 1, (int)Math.Floor((x - _minX) / _cellWidth)));

    private int ClampRow(double y) => Math.Max(0, Math.Min(_rows - 1, (int)Math.Floor((y - _minY) / _cellHeight)));

    /// <summary>Barycentric elevation of a triangle at (x, y); false if the point is outside it or it is degenerate in plan.</summary>
    private static bool TryInterpolate(double[] t, double x, double y, out double z)
    {
        z = 0.0;

        var denominator = (t[4] - t[7]) * (t[0] - t[6]) + (t[6] - t[3]) * (t[1] - t[7]);
        if (Math.Abs(denominator) < 1e-12)
        {
            return false;
        }

        var l0 = ((t[4] - t[7]) * (x - t[6]) + (t[6] - t[3]) * (y - t[7])) / denominator;
        var l1 = ((t[7] - t[1]) * (x - t[6]) + (t[0] - t[6]) * (y - t[7])) / denominator;
        var l2 = 1.0 - l0 - l1;

        if (l0 < -Epsilon || l1 < -Epsilon || l2 < -Epsilon)
        {
            return false;
        }

        z = l0 * t[2] + l1 * t[5] + l2 * t[8];
        return true;
    }
}
