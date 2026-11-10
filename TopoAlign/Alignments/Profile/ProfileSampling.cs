namespace TopoAlign.Alignments.Profile;

/// <summary>Turns a list of profile PVIs into a polyline that follows the straights and the parabolic vertical curves, for drawing.</summary>
internal static class ProfileSampling
{
    /// <summary>Number of segments a vertical curve is drawn with.</summary>
    public const int CurveSegments = 16;

    /// <summary>
    /// Points (distance, elevation) along the profile in order: the first and last PVI, the start and end of every vertical curve
    /// (cut back to fit, as <see cref="ProfileEditing.EffectiveCurveLength"/>) and points along each curve. Feet.
    /// </summary>
    public static List<(double Distance, double Elevation)> Sample(IReadOnlyList<ProfileVertex> vertices)
    {
        var points = new List<(double, double)>();
        if (vertices is null || vertices.Count == 0)
        {
            return points;
        }

        points.Add((vertices[0].Distance, vertices[0].Elevation));
        for (var i = 1; i < vertices.Count - 1; i++)
        {
            var vertex = vertices[i];
            var length = ProfileEditing.EffectiveCurveLength(vertices, i);
            if (length <= 1e-9)
            {
                points.Add((vertex.Distance, vertex.Elevation));
                continue;
            }

            var gradeIn = ProfileEditing.Grade(vertices[i - 1], vertex);
            var gradeOut = ProfileEditing.Grade(vertex, vertices[i + 1]);
            var start = vertex.Distance - length / 2.0;
            var startElevation = vertex.Elevation - gradeIn * length / 2.0;
            for (var step = 0; step <= CurveSegments; step++)
            {
                var x = length * step / CurveSegments;
                var elevation = startElevation + gradeIn * x + (gradeOut - gradeIn) / (2.0 * length) * x * x;
                points.Add((start + x, elevation));
            }
        }

        if (vertices.Count > 1)
        {
            var last = vertices[vertices.Count - 1];
            points.Add((last.Distance, last.Elevation));
        }

        return points;
    }
}
