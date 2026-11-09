namespace TopoAlign.Grading;

internal readonly struct TopoEdgeKey : IEquatable<TopoEdgeKey>, IComparable<TopoEdgeKey>
{
    private readonly long _x;
    private readonly long _y;
    private readonly long _z;

    public TopoEdgeKey(long x, long y, long z)
    {
        _x = x;
        _y = y;
        _z = z;
    }

    public bool Equals(TopoEdgeKey other) => _x == other._x && _y == other._y && _z == other._z;

    public override bool Equals(object? obj) => obj is TopoEdgeKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (int)(_x * 73856093 ^ _y * 19349663 ^ _z * 83492791);
        }
    }

    public int CompareTo(TopoEdgeKey other)
    {
        var c = _x.CompareTo(other._x);
        if (c != 0)
        {
            return c;
        }

        c = _y.CompareTo(other._y);
        return c != 0 ? c : _z.CompareTo(other._z);
    }
}
