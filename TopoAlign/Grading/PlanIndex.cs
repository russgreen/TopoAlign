namespace TopoAlign.Grading;

/// <summary>
/// Remembers points by their plan position so a new point can be checked for being too close to one already kept.
/// A toposolid can't hold points that nearly coincide, and on the inside of a bend the slopes from neighbouring points bunch up.
/// </summary>
internal sealed class PlanIndex
{
    private readonly double _distance;
    private readonly Dictionary<(long Column, long Row), List<(double X, double Y)>> _cells = new();

    public PlanIndex(double distance)
    {
        if (!(distance > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "The distance must be greater than zero.");
        }

        _distance = distance;
    }

    public void Add(double x, double y)
    {
        var key = CellOf(x, y);
        if (!_cells.TryGetValue(key, out var points))
        {
            points = new List<(double X, double Y)>();
            _cells[key] = points;
        }

        points.Add((x, y));
    }

    /// <summary>True if a point already added is within the distance of (x, y) in plan.</summary>
    public bool HasNear(double x, double y)
    {
        var (column, row) = CellOf(x, y);
        for (var c = column - 1; c <= column + 1; c++)
        {
            for (var r = row - 1; r <= row + 1; r++)
            {
                if (!_cells.TryGetValue((c, r), out var points))
                {
                    continue;
                }

                foreach (var (px, py) in points)
                {
                    var dx = px - x;
                    var dy = py - y;
                    if (dx * dx + dy * dy <= _distance * _distance)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Adds the point and returns true if nothing was near it; otherwise leaves the index as it was and returns false.</summary>
    public bool TryAdd(double x, double y)
    {
        if (HasNear(x, y))
        {
            return false;
        }

        Add(x, y);
        return true;
    }

    private (long Column, long Row) CellOf(double x, double y) => ((long)Math.Floor(x / _distance), (long)Math.Floor(y / _distance));
}
