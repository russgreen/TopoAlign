using TopoAlign.Alignments;

namespace TopoAlign.Grading.Slope;

/// <summary>One point along the line being graded, and where its sloped ray meets the surface.</summary>
internal sealed class GradeStation
{
    public GradeStation(GradePoint edge, double directionX, double directionY, double perpendicularFactor)
    {
        Edge = edge;
        DirectionX = directionX;
        DirectionY = directionY;
        PerpendicularFactor = perpendicularFactor;
    }

    public GradePoint Edge { get; }

    /// <summary>Unit horizontal direction the ray travels in.</summary>
    public double DirectionX { get; }

    public double DirectionY { get; }

    /// <summary>
    /// Cosine of the angle between the ray and the line's normal: horizontal travel along the ray
    /// times this is the distance measured square to the line, which is what the slope is set against.
    /// </summary>
    public double PerpendicularFactor { get; }

    public bool Hit { get; set; }

    /// <summary>True when the slope's daylight point is left out because it would bunch up or cross its neighbours.</summary>
    public bool DaylightSkipped { get; set; }

    /// <summary>Why the station has no slope, when <see cref="Hit"/> is false.</summary>
    public MissReason Miss { get; set; }

    /// <summary>
    /// True when the slope ran off the edge of the surface before meeting the ground, so it ends on the edge (at the slope's height)
    /// rather than at the ground. <see cref="Daylight"/> is that point on the edge.
    /// </summary>
    public bool Clipped { get; set; }

    /// <summary>True when the ground is above the line, so the slope rises to meet it; false when it falls away.</summary>
    public bool IsCut { get; set; }

    public GradePoint Daylight { get; set; }
}
