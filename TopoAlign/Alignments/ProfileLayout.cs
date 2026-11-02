namespace TopoAlign.Alignments;

/// <summary>A straight (constant grade) profile segment between two station/elevation points.</summary>
internal sealed class ProfileLine
{
    public ProfileLine(ProfilePoint start, ProfilePoint end)
    {
        Start = start;
        End = end;
    }

    public ProfilePoint Start { get; }
    public ProfilePoint End { get; }
}

/// <summary>A parabolic vertical curve, defined by its tangent points and the point of vertical intersection.</summary>
internal sealed class ProfileParabola
{
    public ProfileParabola(ProfilePoint start, ProfilePoint pvi, ProfilePoint end)
    {
        Start = start;
        Pvi = pvi;
        End = end;
    }

    public ProfilePoint Start { get; }
    public ProfilePoint Pvi { get; }
    public ProfilePoint End { get; }
}

/// <summary>Turns a PVI list into the connected sequence of lines and vertical curves that make up a profile.</summary>
internal static class ProfileLayout
{
    private const double Eps = 1e-9;

    /// <summary>
    /// Builds the profile elements (<see cref="ProfileLine"/> and <see cref="ProfileParabola"/>) for ordered PVIs.
    /// With <paramref name="minVerticalCurveLength"/> &gt; 0 each interior PVI where the grade changes gets a symmetric
    /// parabola of at least that length, reduced where needed so neighbouring curves never overlap
    /// (each curve uses at most half of each adjoining tangent). With 0 the profile is a polyline.
    /// </summary>
    public static List<object> Create(IReadOnlyList<ProfilePoint> pvis, double minVerticalCurveLength)
    {
        return Create(pvis, null, minVerticalCurveLength);
    }

    /// <summary>
    /// As <see cref="Create(IReadOnlyList{ProfilePoint}, double)"/> but with a curve length for each PVI
    /// (<paramref name="curveLengths"/> is indexed like <paramref name="pvis"/>; the first and last entries are ignored).
    /// </summary>
    public static List<object> Create(IReadOnlyList<ProfilePoint> pvis, IReadOnlyList<double> curveLengths)
    {
        if (curveLengths is null)
        {
            throw new ArgumentNullException(nameof(curveLengths));
        }

        return Create(pvis, curveLengths, 0.0);
    }

    private static List<object> Create(IReadOnlyList<ProfilePoint> pvis, IReadOnlyList<double>? curveLengths, double minVerticalCurveLength)
    {
        if (pvis is null || pvis.Count < 2)
        {
            throw new ArgumentException("A profile needs at least two points.", nameof(pvis));
        }

        if (curveLengths is not null && curveLengths.Count != pvis.Count)
        {
            throw new ArgumentException("There must be one curve length for each profile point.", nameof(curveLengths));
        }

        for (var i = 1; i < pvis.Count; i++)
        {
            if (pvis[i].Station - pvis[i - 1].Station <= Eps)
            {
                throw new ArgumentException("Profile stations must be strictly increasing.", nameof(pvis));
            }
        }

        var elements = new List<object>();
        var cursor = pvis[0];

        for (var i = 1; i < pvis.Count - 1; i++)
        {
            var previous = pvis[i - 1];
            var pvi = pvis[i];
            var next = pvis[i + 1];

            var gradeIn = (pvi.Elevation - previous.Elevation) / (pvi.Station - previous.Station);
            var gradeOut = (next.Elevation - pvi.Elevation) / (next.Station - pvi.Station);

            var half = HalfLength(curveLengths is null ? minVerticalCurveLength : curveLengths[i], pvi.Station - previous.Station, next.Station - pvi.Station);
            if (half <= Eps || Math.Abs(gradeOut - gradeIn) <= Eps)
            {
                AddLine(elements, cursor, pvi);
                cursor = pvi;
                continue;
            }

            var curveStart = new ProfilePoint(pvi.Station - half, pvi.Elevation - gradeIn * half);
            var curveEnd = new ProfilePoint(pvi.Station + half, pvi.Elevation + gradeOut * half);

            AddLine(elements, cursor, curveStart);
            elements.Add(new ProfileParabola(curveStart, pvi, curveEnd));
            cursor = curveEnd;
        }

        AddLine(elements, cursor, pvis[pvis.Count - 1]);
        return elements;
    }

    private static double HalfLength(double minVerticalCurveLength, double tangentIn, double tangentOut)
    {
        if (minVerticalCurveLength <= Eps)
        {
            return 0.0;
        }

        return Math.Min(minVerticalCurveLength / 2.0, 0.5 * Math.Min(tangentIn, tangentOut));
    }

    private static void AddLine(List<object> elements, ProfilePoint start, ProfilePoint end)
    {
        if (end.Station - start.Station > Eps)
        {
            elements.Add(new ProfileLine(start, end));
        }
    }
}
