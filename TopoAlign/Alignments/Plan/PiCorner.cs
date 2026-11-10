namespace TopoAlign.Alignments.Plan;

internal sealed class PiCorner
{
    public bool HasCurve;
    public double Turn;            // signed deflection angle, left positive
    public double Radius;
    public double SpiralLength;
    public double SpiralAngle;
    public double TangentLength;
}
