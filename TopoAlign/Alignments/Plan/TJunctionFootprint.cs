namespace TopoAlign.Alignments.Plan;

/// <summary>
/// The line across the side road where the junction begins. It is square to the side road, so everything cut or extended to it ends
/// in a straight edge. Side road geometry beyond it (towards the main road) is cut away, and geometry that stops short of it is
/// extended to it.
/// </summary>
internal readonly struct SideJoin
{
    public SideJoin(PlanPoint left, PlanPoint right, PlanPoint normal, PlanPoint direction)
    {
        Left = left;
        Right = right;
        Normal = normal;
        Direction = direction;
    }

    /// <summary>Where the join line meets the side road's left kerb line (left of travel towards the main road).</summary>
    public PlanPoint Left { get; }

    /// <summary>Where the join line meets the side road's right kerb line.</summary>
    public PlanPoint Right { get; }

    /// <summary>Unit vector perpendicular to the join line, pointing towards the main road.</summary>
    public PlanPoint Normal { get; }

    /// <summary>Unit direction of travel along the side road towards the main road.</summary>
    public PlanPoint Direction { get; }

    /// <summary>How far a point is beyond the join line, towards the main road. Negative on the side road's side.</summary>
    public double SignedDistance(PlanPoint p) => (p.X - Left.X) * Normal.X + (p.Y - Left.Y) * Normal.Y;
}

/// <summary>
/// Pure plan geometry for fitting a T-junction into road solids that already exist: the area to clear out of the main road and the
/// line the side road must end on. Both ends are square, to the main road along its edges and to the side road across its end, so
/// kerbs, footways and the rest finish in a straight edge that new geometry can join to.
/// </summary>
internal static class TJunctionFootprint
{
    /// <summary>
    /// The plan area to clear: from the main road kerb line, between the two kerb returns' ends on the main road, straight out
    /// (square to the main road) to the join line across the side road. Counter-clockwise. Null if the layout is not valid or the
    /// join line lies on the wrong side of the main road kerb line.
    /// </summary>
    public static List<PlanPoint>? CutPolygon(TJunctionResult result)
    {
        if (Join(result) is not { } join || result.KerbLeft is null || result.KerbRight is null)
        {
            return null;
        }

        var n = result.MainNormal;
        var d = join.Direction;
        var nd = n.X * d.X + n.Y * d.Y;
        if (Math.Abs(nd) < 1e-9)
        {
            return null;
        }

        var corners = new List<PlanPoint>();
        foreach (var t2 in new[] { result.KerbLeft.EndPoint, result.KerbRight.EndPoint })
        {
            // run from the main kerb line along the main road's normal until the join line is reached
            var s = -join.SignedDistance(t2) / nd;
            if (s < 0.0)
            {
                return null;
            }

            corners.Add(t2);
            corners.Add(new PlanPoint(t2.X + n.X * s, t2.Y + n.Y * s));
        }

        // left start, left far corner, right start, right far corner -> order round the outline
        var polygon = new List<PlanPoint> { corners[0], corners[1], corners[3], corners[2] };
        if (TJunctionSurface.SignedArea(polygon) < 0.0)
        {
            polygon.Reverse();
        }

        return polygon;
    }

    /// <summary>
    /// The line the side road ends on: square to the side road, at the kerb return tangent point that is furthest from the main road,
    /// so both kerb returns start on or beyond it. Null if the layout is not valid or the side road has no width.
    /// </summary>
    public static SideJoin? Join(TJunctionResult result)
    {
        if (!result.IsValid || result.KerbLeft is null || result.KerbRight is null)
        {
            return null;
        }

        var d = result.SideDirection;
        var left = result.KerbLeft.StartPoint;
        var right = result.KerbRight.StartPoint;

        // the join line sits at the smaller of the two distances along the side road, towards the side road
        var along = Math.Min(left.X * d.X + left.Y * d.Y, right.X * d.X + right.Y * d.Y);
        PlanPoint OnLine(PlanPoint p)
        {
            var back = p.X * d.X + p.Y * d.Y - along;
            return new PlanPoint(p.X - d.X * back, p.Y - d.Y * back);
        }

        var a = OnLine(left);
        var b = OnLine(right);
        if (PlanPoint.Distance(a, b) < 1e-9)
        {
            return null; // zero half-width: there is no line to join on
        }

        return new SideJoin(a, b, d, d);
    }
}
