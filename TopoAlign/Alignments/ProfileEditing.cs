namespace TopoAlign.Alignments;

/// <summary>Pure helpers for editing a list of profile PVIs: adding a station, checking the list and reading its grades.</summary>
internal static class ProfileEditing
{
    /// <summary>Stations closer together than this (feet) count as the same station.</summary>
    public const double MinSpacing = 1e-3;

    /// <summary>
    /// The profile's PVIs must start at distance 0 and end at <paramref name="profileLength"/>, be strictly increasing, finite,
    /// and have no negative curve length. Returns user-readable errors, empty when the list is valid.
    /// </summary>
    public static List<string> Validate(IReadOnlyList<ProfileVertex> vertices, double profileLength)
    {
        var errors = new List<string>();
        if (vertices is null || vertices.Count < 2)
        {
            errors.Add("The profile needs at least two stations.");
            return errors;
        }

        if (Math.Abs(vertices[0].Distance) > MinSpacing)
        {
            errors.Add("The first station must stay at the start of the alignment.");
        }

        if (Math.Abs(vertices[vertices.Count - 1].Distance - profileLength) > MinSpacing)
        {
            errors.Add("The last station must stay at the end of the alignment.");
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            var vertex = vertices[i];
            if (double.IsNaN(vertex.Distance) || double.IsInfinity(vertex.Distance) ||
                double.IsNaN(vertex.Elevation) || double.IsInfinity(vertex.Elevation) ||
                double.IsNaN(vertex.CurveLength) || double.IsInfinity(vertex.CurveLength))
            {
                errors.Add($"Station {i + 1} has a value that is not a number.");
                continue;
            }

            if (vertex.CurveLength < 0.0)
            {
                errors.Add($"Station {i + 1} has a negative curve length.");
            }

            if (i > 0 && vertex.Distance - vertices[i - 1].Distance < MinSpacing)
            {
                errors.Add($"Stations {i} and {i + 1} are in the same place or out of order.");
            }
        }

        return errors;
    }

    /// <summary>
    /// The elevation of the straight-line profile through the PVIs at <paramref name="distance"/>. A station added there with
    /// this elevation leaves the grade lines unchanged.
    /// </summary>
    public static double ElevationAt(IReadOnlyList<ProfileVertex> vertices, double distance)
    {
        if (vertices is null || vertices.Count == 0)
        {
            throw new ArgumentException("There are no profile points.", nameof(vertices));
        }

        if (distance <= vertices[0].Distance)
        {
            return vertices[0].Elevation;
        }

        for (var i = 1; i < vertices.Count; i++)
        {
            if (distance <= vertices[i].Distance)
            {
                var previous = vertices[i - 1];
                var next = vertices[i];
                var t = (distance - previous.Distance) / (next.Distance - previous.Distance);
                return previous.Elevation + t * (next.Elevation - previous.Elevation);
            }
        }

        return vertices[vertices.Count - 1].Elevation;
    }

    /// <summary>
    /// Returns a copy of the list with a new station at <paramref name="distance"/>, in order, lying on the existing grade lines.
    /// Throws <see cref="InvalidOperationException"/> if the distance is not strictly inside the profile or is already a station.
    /// </summary>
    public static List<ProfileVertex> Insert(IReadOnlyList<ProfileVertex> vertices, double distance, double curveLength)
    {
        if (vertices is null || vertices.Count < 2)
        {
            throw new InvalidOperationException("The profile needs at least two stations.");
        }

        var first = vertices[0].Distance;
        var last = vertices[vertices.Count - 1].Distance;
        if (distance <= first + MinSpacing || distance >= last - MinSpacing)
        {
            throw new InvalidOperationException("A new station must lie between the start and the end of the alignment.");
        }

        if (vertices.Any(v => Math.Abs(v.Distance - distance) < MinSpacing))
        {
            throw new InvalidOperationException("There is already a station there.");
        }

        var result = new List<ProfileVertex>(vertices.Count + 1);
        var added = false;
        foreach (var vertex in vertices)
        {
            if (!added && vertex.Distance > distance)
            {
                result.Add(new ProfileVertex(distance, ElevationAt(vertices, distance), Math.Max(curveLength, 0.0)));
                added = true;
            }

            result.Add(vertex);
        }

        return result;
    }

    /// <summary>Grade (rise over run) of the straight between two consecutive PVIs.</summary>
    public static double Grade(ProfileVertex from, ProfileVertex to)
    {
        return (to.Elevation - from.Elevation) / (to.Distance - from.Distance);
    }

    /// <summary>
    /// Formats a grade as a percentage followed, in brackets, by the same gradient as a ratio, e.g. "1.25 % (1:80)". A flat
    /// grade has no ratio, so only the percentage is returned.
    /// </summary>
    public static string FormatGrade(double grade)
    {
        var percent = grade.ToString("P2");
        if (Math.Abs(grade) < 1e-9)
        {
            return percent;
        }

        return $"{percent} (1:{(1.0 / Math.Abs(grade)).ToString("0.#")})";
    }

    /// <summary>Largest absolute grade between consecutive PVIs.</summary>
    public static double MaxAbsGrade(IReadOnlyList<ProfileVertex> vertices)
    {
        var max = 0.0;
        for (var i = 1; i < vertices.Count; i++)
        {
            max = Math.Max(max, Math.Abs(Grade(vertices[i - 1], vertices[i])));
        }

        return max;
    }

    /// <summary>
    /// The length of the vertical curve that is actually written at an interior PVI: the requested length, reduced so that it
    /// uses at most half of each neighbouring straight, and 0 where the grade does not change.
    /// </summary>
    public static double EffectiveCurveLength(IReadOnlyList<ProfileVertex> vertices, int index)
    {
        if (index <= 0 || index >= vertices.Count - 1)
        {
            return 0.0;
        }

        var before = vertices[index - 1];
        var vertex = vertices[index];
        var after = vertices[index + 1];
        if (vertex.CurveLength <= 1e-9 || Math.Abs(Grade(vertex, after) - Grade(before, vertex)) <= 1e-9)
        {
            return 0.0;
        }

        var half = Math.Min(vertex.CurveLength / 2.0, 0.5 * Math.Min(vertex.Distance - before.Distance, after.Distance - vertex.Distance));
        return 2.0 * half;
    }
}