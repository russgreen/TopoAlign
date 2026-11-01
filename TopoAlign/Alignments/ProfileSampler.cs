using Autodesk.Revit.DB;

namespace TopoAlign.Alignments;

/// <summary>Samples ground elevations along an alignment from an elevation lookup.</summary>
internal static class ProfileSampler
{
    /// <summary>Smallest sampling interval allowed, in feet, so a zero interval can't loop forever.</summary>
    private const double MinInterval = 0.01;

    /// <summary>Ground elevations sampled along the alignment, in feet, plus how many stations missed the surface.</summary>
    internal sealed class SampleResult
    {
        public SampleResult(List<ProfilePoint> samples, int missingCount)
        {
            Samples = samples;
            MissingCount = missingCount;
        }

        public List<ProfilePoint> Samples { get; }
        public int MissingCount { get; }
    }

    /// <summary>
    /// Samples the surface along a chain of lines/arcs every <paramref name="interval"/> feet, and at the end of
    /// every curve. Stations are plan distances from the start of the chain and match the alignment's stationing.
    /// Stations that fall off the surface are skipped (the profile is bridged across them), except that the
    /// first and last station must hit it.
    /// </summary>
    /// <param name="elevationOffset">Added to every ground elevation (feet); positive lifts the profile above the ground.</param>
    public static SampleResult Sample(IReadOnlyList<Curve> curves, TriangleSurface surface, double interval, double elevationOffset)
    {
        if (curves is null || curves.Count == 0)
        {
            throw new ArgumentException("At least one curve is required.", nameof(curves));
        }

        var step = Math.Max(interval, MinInterval);
        var samples = new List<ProfilePoint>();
        var missing = 0;
        var curveStart = 0.0;
        var startSampled = false;

        for (var index = 0; index < curves.Count; index++)
        {
            var curve = curves[index];
            var length = AlignmentJsonBuilder.PlanLength(curve);
            if (length <= 1e-9)
            {
                continue;
            }

            // Stations along this curve: every step, plus its end. Only the first curve with length also samples its
            // start; for the rest the start is the previous curve's end.
            var distances = new List<double>();
            if (!startSampled)
            {
                distances.Add(0.0);
                startSampled = true;
            }

            for (var d = step; d < length - 1e-9; d += step)
            {
                distances.Add(d);
            }

            distances.Add(length);

            foreach (var distance in distances)
            {
                var point = curve.Evaluate(Math.Min(1.0, distance / length), true);
                if (surface.TryGetElevation(point.X, point.Y, out var ground))
                {
                    samples.Add(new ProfilePoint(curveStart + distance, ground + elevationOffset));
                }
                else
                {
                    missing++;
                }
            }

            curveStart += length;
        }

        if (samples.Count < 2)
        {
            throw new InvalidOperationException("Most of the alignment is outside the selected toposolid, so a profile can't be draped on it.");
        }

        if (samples[0].Station > 1e-6 || samples[samples.Count - 1].Station < curveStart - 1e-6)
        {
            throw new InvalidOperationException("The start or end of the alignment is outside the selected toposolid, so a profile can't be draped on it.");
        }

        return new SampleResult(samples, missing);
    }
}
