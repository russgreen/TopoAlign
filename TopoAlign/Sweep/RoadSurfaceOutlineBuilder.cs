namespace TopoAlign.Sweep;

/// <summary>
/// Collects the triangles of a road's up-facing surface and works out its outline. The outline is the set of edges that
/// belong to one triangle only, which avoids needing a 2D union of the many faces a lofted road is made from.
/// </summary>
internal sealed class RoadSurfaceOutlineBuilder
{
    /// <summary>Corners closer than this in plan are the same point, in feet (about 0.3 mm).</summary>
    internal const double WeldTolerance = 0.001;

    /// <summary>Outline segments shorter than this are merged away; Revit refuses curves shorter than about 0.0026 ft.</summary>
    internal const double MinSegment = 0.005;

    /// <summary>An outline point this close to the line between its neighbours is dropped.</summary>
    internal const double StraightTolerance = 0.0005;

    private const double CellSize = 1.0;
    private const int MaxSplitCells = 250000;

    private readonly List<double> _x = new();
    private readonly List<double> _y = new();
    private readonly List<double> _z = new();
    private readonly Dictionary<(long, long), List<int>> _weldCells = new();
    private readonly List<(int A, int B, int C)> _triangles = new();

    public int TriangleCount => _triangles.Count;

    public void AddTriangle(double x0, double y0, double z0, double x1, double y1, double z1, double x2, double y2, double z2)
    {
        var a = Weld(x0, y0, z0);
        var b = Weld(x1, y1, z1);
        var c = Weld(x2, y2, z2);
        if (a == b || b == c || a == c)
        {
            return;
        }

        // a triangle standing on edge has no area in plan and nothing to say about the outline
        var cross = (_x[b] - _x[a]) * (_y[c] - _y[a]) - (_x[c] - _x[a]) * (_y[b] - _y[a]);
        if (Math.Abs(cross) < 1e-10)
        {
            return;
        }

        _triangles.Add((a, b, c));
    }

    public RoadSurfaceOutline Build()
    {
        var points = new List<SurfaceVertex>();
        var used = new HashSet<int>();
        foreach (var (a, b, c) in _triangles)
        {
            foreach (var index in new[] { a, b, c })
            {
                if (used.Add(index))
                {
                    points.Add(new SurfaceVertex(_x[index], _y[index], _z[index]));
                }
            }
        }

        var boundary = FindBoundary();
        var loops = Chain(boundary, out var openChains);
        return new RoadSurfaceOutline(points, loops, openChains);
    }

    // ---- welding -------------------------------------------------------------------------------------------------

    private int Weld(double x, double y, double z)
    {
        var cx = (long)Math.Floor(x / WeldTolerance);
        var cy = (long)Math.Floor(y / WeldTolerance);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (!_weldCells.TryGetValue((cx + dx, cy + dy), out var cell))
                {
                    continue;
                }

                foreach (var index in cell)
                {
                    if (Math.Abs(_x[index] - x) <= WeldTolerance && Math.Abs(_y[index] - y) <= WeldTolerance)
                    {
                        if (z > _z[index])
                        {
                            _z[index] = z;
                        }

                        return index;
                    }
                }
            }
        }

        var added = _x.Count;
        _x.Add(x);
        _y.Add(y);
        _z.Add(z);
        if (!_weldCells.TryGetValue((cx, cy), out var list))
        {
            list = new List<int>();
            _weldCells[(cx, cy)] = list;
        }

        list.Add(added);
        return added;
    }

    // ---- boundary edges ------------------------------------------------------------------------------------------

    private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    private static (int A, int B) EdgeOf(long key) => ((int)(key >> 32), (int)(key & 0xFFFFFFFF));

    /// <summary>The edges used by one triangle only, with any corner that lies along an edge (where the neighbouring face was cut differently) split in.</summary>
    private List<(int A, int B)> FindBoundary()
    {
        var counts = new Dictionary<long, int>();
        foreach (var (a, b, c) in _triangles)
        {
            Count(counts, a, b);
            Count(counts, b, c);
            Count(counts, c, a);
        }

        var candidates = counts.Where(pair => pair.Value == 1).Select(pair => EdgeOf(pair.Key)).ToList();
        if (candidates.Count == 0)
        {
            return candidates;
        }

        // Faces next to each other don't always cut a shared edge at the same places, so a long edge on one side
        // faces two short ones on the other. Splitting at the corners that lie on it lets them cancel.
        var grid = new Dictionary<(long, long), List<int>>();
        foreach (var vertex in candidates.SelectMany(edge => new[] { edge.A, edge.B }).Distinct())
        {
            var key = ((long)Math.Floor(_x[vertex] / CellSize), (long)Math.Floor(_y[vertex] / CellSize));
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<int>();
                grid[key] = list;
            }

            list.Add(vertex);
        }

        var pieceCounts = new Dictionary<long, int>();
        foreach (var (a, b) in candidates)
        {
            foreach (var (start, end) in Split(a, b, grid))
            {
                Count(pieceCounts, start, end);
            }
        }

        return pieceCounts.Where(pair => pair.Value % 2 == 1).Select(pair => EdgeOf(pair.Key)).ToList();
    }

    private static void Count(Dictionary<long, int> counts, int a, int b)
    {
        var key = EdgeKey(a, b);
        counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    private List<(int Start, int End)> Split(int a, int b, Dictionary<(long, long), List<int>> grid)
    {
        var dx = _x[b] - _x[a];
        var dy = _y[b] - _y[a];
        var lengthSquared = dx * dx + dy * dy;

        var minCx = (long)Math.Floor((Math.Min(_x[a], _x[b]) - WeldTolerance) / CellSize);
        var maxCx = (long)Math.Floor((Math.Max(_x[a], _x[b]) + WeldTolerance) / CellSize);
        var minCy = (long)Math.Floor((Math.Min(_y[a], _y[b]) - WeldTolerance) / CellSize);
        var maxCy = (long)Math.Floor((Math.Max(_y[a], _y[b]) + WeldTolerance) / CellSize);

        var onEdge = new List<(double T, int Vertex)>();
        if ((maxCx - minCx + 1) * (maxCy - minCy + 1) <= MaxSplitCells)
        {
            for (var cx = minCx; cx <= maxCx; cx++)
            {
                for (var cy = minCy; cy <= maxCy; cy++)
                {
                    if (!grid.TryGetValue((cx, cy), out var cell))
                    {
                        continue;
                    }

                    foreach (var vertex in cell)
                    {
                        if (vertex == a || vertex == b)
                        {
                            continue;
                        }

                        var t = ((_x[vertex] - _x[a]) * dx + (_y[vertex] - _y[a]) * dy) / lengthSquared;
                        var along = Math.Sqrt(lengthSquared) * Math.Min(t, 1.0 - t);
                        if (t <= 0.0 || t >= 1.0 || along <= WeldTolerance)
                        {
                            continue;
                        }

                        var offX = _x[a] + t * dx - _x[vertex];
                        var offY = _y[a] + t * dy - _y[vertex];
                        if (offX * offX + offY * offY <= WeldTolerance * WeldTolerance)
                        {
                            onEdge.Add((t, vertex));
                        }
                    }
                }
            }
        }

        var pieces = new List<(int, int)>();
        var previous = a;
        foreach (var (_, vertex) in onEdge.OrderBy(item => item.T))
        {
            if (vertex != previous)
            {
                pieces.Add((previous, vertex));
                previous = vertex;
            }
        }

        pieces.Add((previous, b));
        return pieces;
    }

    // ---- outline loops -------------------------------------------------------------------------------------------

    private List<List<SurfaceVertex>> Chain(List<(int A, int B)> edges, out int openChains)
    {
        openChains = 0;
        var neighbours = new Dictionary<int, List<int>>();
        foreach (var (a, b) in edges)
        {
            AddNeighbour(neighbours, a, b);
            AddNeighbour(neighbours, b, a);
        }

        var usedEdges = new HashSet<long>();
        var loops = new List<List<SurfaceVertex>>();
        foreach (var (a, b) in edges)
        {
            if (!usedEdges.Add(EdgeKey(a, b)))
            {
                continue;
            }

            var chain = new List<int> { a, b };
            var current = b;
            var closed = false;
            while (true)
            {
                if (current == a)
                {
                    chain.RemoveAt(chain.Count - 1);
                    closed = true;
                    break;
                }

                var next = -1;
                foreach (var candidate in neighbours[current])
                {
                    if (usedEdges.Add(EdgeKey(current, candidate)))
                    {
                        next = candidate;
                        break;
                    }
                }

                if (next < 0)
                {
                    break;
                }

                chain.Add(next);
                current = next;
            }

            if (!closed)
            {
                openChains++;
                continue;
            }

            var cleaned = Clean(chain);
            if (cleaned.Count >= 3)
            {
                loops.Add(cleaned);
            }
        }

        return loops;
    }

    private static void AddNeighbour(Dictionary<int, List<int>> neighbours, int vertex, int other)
    {
        if (!neighbours.TryGetValue(vertex, out var list))
        {
            list = new List<int>();
            neighbours[vertex] = list;
        }

        list.Add(other);
    }

    /// <summary>Drops points that are too close to the last one, or that sit on the straight line between their neighbours.</summary>
    private List<SurfaceVertex> Clean(List<int> chain)
    {
        var points = chain.Select(index => new SurfaceVertex(_x[index], _y[index], _z[index])).ToList();

        for (var pass = 0; pass < 3; pass++)
        {
            var before = points.Count;

            var spaced = new List<SurfaceVertex>();
            foreach (var point in points)
            {
                if (spaced.Count == 0 || Distance(spaced[spaced.Count - 1], point) >= MinSegment)
                {
                    spaced.Add(point);
                }
            }

            while (spaced.Count > 1 && Distance(spaced[spaced.Count - 1], spaced[0]) < MinSegment)
            {
                spaced.RemoveAt(spaced.Count - 1);
            }

            var straight = new List<SurfaceVertex>();
            for (var i = 0; i < spaced.Count; i++)
            {
                var previous = straight.Count > 0 ? straight[straight.Count - 1] : spaced[spaced.Count - 1];
                var next = spaced[(i + 1) % spaced.Count];
                if (spaced.Count > 3 && DistanceToLine(spaced[i], previous, next) < StraightTolerance)
                {
                    continue;
                }

                straight.Add(spaced[i]);
            }

            points = straight;
            if (points.Count == before || points.Count < 3)
            {
                break;
            }
        }

        return points;
    }

    private static double Distance(SurfaceVertex a, SurfaceVertex b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistanceToLine(SurfaceVertex point, SurfaceVertex lineStart, SurfaceVertex lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-12)
        {
            return Distance(point, lineStart);
        }

        return Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / length;
    }
}
