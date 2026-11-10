namespace TopoAlign.Grading;

/// <summary>Plan (X, Y) measurements on grading points; heights are ignored.</summary>
internal static class PlanMath
{
    /// <summary>The straight-line distance between two points in plan.</summary>
    public static double Distance(GradePoint a, GradePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
