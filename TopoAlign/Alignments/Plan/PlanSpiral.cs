namespace TopoAlign.Alignments.Plan;

/// <summary>
/// A clothoid (Euler spiral): curvature changes linearly with distance from <c>startCurvature</c> to
/// <c>endCurvature</c> (1/radius, positive for a left turn). A transition into a curve starts at 0.
/// </summary>
internal sealed class PlanSpiral : PlanSegment
{
    private const double IntegrationStep = 0.25;

    private readonly PlanPoint _start;
    private readonly double _startHeading;
    private readonly double _startCurvature;
    private readonly double _endCurvature;
    private readonly double _length;

    public PlanSpiral(PlanPoint start, double startHeading, double startCurvature, double endCurvature, double length)
    {
        if (length <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A spiral needs a length greater than zero.");
        }

        _start = start;
        _startHeading = startHeading;
        _startCurvature = startCurvature;
        _endCurvature = endCurvature;
        _length = length;
    }

    public double StartCurvature => _startCurvature;
    public double EndCurvature => _endCurvature;
    public double StartHeading => _startHeading;

    public override double Length => _length;
    public override PlanPoint StartPoint => _start;
    public override PlanPoint EndPoint => PointAt(_length);

    public override double HeadingAt(double distance)
    {
        var d = Math.Max(0.0, Math.Min(_length, distance));
        return _startHeading + _startCurvature * d + (_endCurvature - _startCurvature) * d * d / (2.0 * _length);
    }

    /// <summary>Integrates the heading numerically (Simpson's rule); the step is far below the tolerance of the layout.</summary>
    public override PlanPoint PointAt(double distance)
    {
        var d = Math.Max(0.0, Math.Min(_length, distance));
        if (d <= 0.0)
        {
            return _start;
        }

        var steps = Math.Max(8, (int)Math.Ceiling(d / IntegrationStep));
        if (steps % 2 == 1)
        {
            steps++;
        }

        var h = d / steps;
        double sumX = 0.0, sumY = 0.0;
        for (var i = 0; i <= steps; i++)
        {
            var weight = i == 0 || i == steps ? 1.0 : (i % 2 == 1 ? 4.0 : 2.0);
            var heading = HeadingAt(i * h);
            sumX += weight * Math.Cos(heading);
            sumY += weight * Math.Sin(heading);
        }

        return new PlanPoint(_start.X + sumX * h / 3.0, _start.Y + sumY * h / 3.0);
    }

    public override PlanSegment Reversed()
    {
        // travelling the other way flips the heading by 180 degrees and the sign of the curvature
        return new PlanSpiral(EndPoint, HeadingAt(_length) + Math.PI, -_endCurvature, -_startCurvature, _length);
    }

    /// <summary>
    /// Points along the spiral so that the chord between consecutive points deviates from the curve by no more
    /// than <paramref name="maxDeviation"/> (feet). Includes both ends.
    /// </summary>
    public List<PlanPoint> Tessellate(double maxDeviation)
    {
        var tolerance = Math.Max(maxDeviation, 1e-6);
        var maxCurvature = Math.Max(Math.Abs(_startCurvature), Math.Abs(_endCurvature));
        var pieceLength = maxCurvature <= 1e-12 ? _length : Math.Sqrt(8.0 * tolerance / maxCurvature);
        var pieces = (int)Math.Ceiling(_length / Math.Max(pieceLength, 1e-3));
        pieces = Math.Max(4, Math.Min(pieces, 500));

        var points = new List<PlanPoint>(pieces + 1);
        for (var i = 0; i <= pieces; i++)
        {
            points.Add(i == 0 ? _start : PointAt(_length * i / pieces));
        }

        return points;
    }
}
