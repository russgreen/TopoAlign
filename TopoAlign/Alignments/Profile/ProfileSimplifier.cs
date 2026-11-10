namespace TopoAlign.Alignments.Profile;

/// <summary>Reduces a densely sampled ground profile to a small set of PVIs.</summary>
internal static class ProfileSimplifier
{
    /// <summary>
    /// Douglas-Peucker simplification of a station/elevation series. A sample is dropped when the profile
    /// without it stays within <paramref name="verticalTolerance"/> of it (measured vertically).
    /// The first and last samples are always kept. Input must be ordered by station.
    /// </summary>
    public static List<ProfilePoint> Simplify(IReadOnlyList<ProfilePoint> samples, double verticalTolerance)
    {
        if (samples is null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        if (samples.Count <= 2)
        {
            return samples.ToList();
        }

        var tolerance = Math.Max(verticalTolerance, 0.0);
        var keep = new bool[samples.Count];
        keep[0] = true;
        keep[samples.Count - 1] = true;

        var pending = new Stack<(int First, int Last)>();
        pending.Push((0, samples.Count - 1));

        while (pending.Count > 0)
        {
            var (first, last) = pending.Pop();
            if (last - first < 2)
            {
                continue;
            }

            var worstIndex = -1;
            var worstDistance = -1.0;
            for (var i = first + 1; i < last; i++)
            {
                var distance = VerticalDistance(samples[first], samples[last], samples[i]);
                if (distance > worstDistance)
                {
                    worstDistance = distance;
                    worstIndex = i;
                }
            }

            if (worstDistance > tolerance)
            {
                keep[worstIndex] = true;
                pending.Push((first, worstIndex));
                pending.Push((worstIndex, last));
            }
        }

        var result = new List<ProfilePoint>();
        for (var i = 0; i < samples.Count; i++)
        {
            if (keep[i])
            {
                result.Add(samples[i]);
            }
        }

        return result;
    }

    /// <summary>Vertical distance from <paramref name="point"/> to the chord between <paramref name="start"/> and <paramref name="end"/>.</summary>
    internal static double VerticalDistance(ProfilePoint start, ProfilePoint end, ProfilePoint point)
    {
        var run = end.Station - start.Station;
        if (Math.Abs(run) < 1e-12)
        {
            return Math.Abs(point.Elevation - start.Elevation);
        }

        var t = (point.Station - start.Station) / run;
        var chordElevation = start.Elevation + (end.Elevation - start.Elevation) * t;
        return Math.Abs(point.Elevation - chordElevation);
    }

    /// <summary>Largest absolute grade (rise over run) between consecutive PVIs, or 0 if there are fewer than two.</summary>
    public static double MaxAbsGrade(IReadOnlyList<ProfilePoint> pvis)
    {
        var max = 0.0;
        for (var i = 1; i < pvis.Count; i++)
        {
            var run = pvis[i].Station - pvis[i - 1].Station;
            if (run > 1e-12)
            {
                max = Math.Max(max, Math.Abs((pvis[i].Elevation - pvis[i - 1].Elevation) / run));
            }
        }

        return max;
    }
}