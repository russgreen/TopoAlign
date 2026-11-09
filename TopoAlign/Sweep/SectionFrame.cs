using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>Places section points on the path of an alignment.</summary>
internal static class SectionFrame
{
    /// <summary>
    /// The world position of a section point. The section stands in a vertical plane across the path, so the "up" of the
    /// section is always world up and only its direction across the road follows the path: u runs to the right of the
    /// direction of travel (<paramref name="tangentX"/>, <paramref name="tangentY"/> need not be a unit vector).
    /// </summary>
    /// <param name="verticalOffset">Added to every point's height, in feet.</param>
    public static PathPoint ToWorld(double u, double v, PathPoint centre, double tangentX, double tangentY, double verticalOffset)
    {
        var length = Math.Sqrt(tangentX * tangentX + tangentY * tangentY);
        if (length < 1e-12)
        {
            throw new ArgumentException("The path direction is undefined at this station.", nameof(tangentX));
        }

        var rightX = tangentY / length;
        var rightY = -tangentX / length;
        return new PathPoint(centre.X + u * rightX, centre.Y + u * rightY, centre.Z + v + verticalOffset);
    }
}
