namespace TopoAlign.Geometry;

/// <summary>Distance tolerances shared by the geometry, grading and comparer code. All values are in feet.</summary>
internal static class Tolerances
{
    /// <summary>One sixteenth of an inch, the plan tolerance used when treating two points as the same.</summary>
    public const double SixteenthInchInFeet = 1.0d / (16.0d * 12.0d);
}
