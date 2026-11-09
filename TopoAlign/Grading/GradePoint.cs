using TopoAlign.Alignments;

namespace TopoAlign.Grading;

internal readonly struct GradePoint
{
    public GradePoint(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}
