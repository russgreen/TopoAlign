using static TopoAlign.Tests.TestKit;
using TopoAlign.Alignments;
using TopoAlign.Sweep;
using TopoAlign.Alignments.Plan;
using TopoAlign.Sweep.Profiles;

namespace TopoAlign.Tests;

public class StationPlannerTests : TopoAlignTest
{
    private static PathPoint Straight(double station) => new PathPoint(station, 0, 0);

    /// <summary>A circle of the given radius, walked at one foot per station.</summary>
    private static Func<double, PathPoint> CircleOfRadius(double radius) =>
        station => new PathPoint(radius * Math.Cos(station / radius), radius * Math.Sin(station / radius), 0);

    // ---- Plan ---------------------------------------------------------------------------------------------------

    [Test]
    public async Task Plan_EvenlySpaced_IncludesBothEnds()
    {
        var stations = StationPlanner.Plan(0, 100, 25);

        await Assert.That(stations.Count).IsEqualTo(5);
        await Assert.That(stations[0]).IsEqualTo(0.0);
        await Assert.That(stations[2]).IsEqualTo(50.0).Within(1e-9);
        await Assert.That(stations[4]).IsEqualTo(100.0);
    }

    [Test]
    public async Task Plan_IntervalThatDoesNotDivideTheLength_StillEndsOnTheEnd()
    {
        var stations = StationPlanner.Plan(0, 100, 30);

        await Assert.That(stations.Count).IsEqualTo(5);
        await Assert.That(stations[3]).IsEqualTo(90.0).Within(1e-9);
        await Assert.That(stations[4]).IsEqualTo(100.0);
    }

    [Test]
    public async Task Plan_ARegularStationJustBeforeTheEnd_IsDropped()
    {
        var stations = StationPlanner.Plan(0, 100, 33);

        // 99 would leave a sliver of 1 ft before the end
        await Assert.That(stations.Count).IsEqualTo(4);
        await Assert.That(stations[2]).IsEqualTo(66.0).Within(1e-9);
        await Assert.That(stations[3]).IsEqualTo(100.0);
    }

    [Test]
    public async Task Plan_MandatoryStations_AreKept_AndCrowdedRegularOnesDropped()
    {
        var stations = StationPlanner.Plan(0, 100, 25, new[] { 24.0 });

        await Assert.That(stations.Contains(24.0)).IsTrue();
        await Assert.That(stations.Contains(25.0)).IsFalse();
        await Assert.That(stations.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Plan_MandatoryStationsOutsideTheRange_AreIgnored()
    {
        var stations = StationPlanner.Plan(0, 100, 50, new[] { -10.0, 0.0, 100.0, 140.0 });

        await Assert.That(stations.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Plan_DuplicateMandatoryStations_AppearOnce()
    {
        var stations = StationPlanner.Plan(0, 100, 50, new[] { 30.0, 30.0, 30.0000001 });

        await Assert.That(stations.Count(s => Math.Abs(s - 30.0) < 1e-3)).IsEqualTo(1);
    }

    [Test]
    public async Task Plan_StartsWherever_TheAlignmentStarts()
    {
        var stations = StationPlanner.Plan(500, 600, 25);

        await Assert.That(stations[0]).IsEqualTo(500.0);
        await Assert.That(stations[stations.Count - 1]).IsEqualTo(600.0);
        await Assert.That(stations.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Plan_IsStrictlyIncreasing()
    {
        var stations = StationPlanner.Plan(0, 1000, 7.3, new[] { 100.0, 250.5, 250.6, 999.9 });

        for (var i = 1; i < stations.Count; i++)
        {
            await Assert.That(stations[i]).IsGreaterThan(stations[i - 1]);
        }
    }

    [Test]
    public async Task Plan_EndNotAfterStart_Throws()
    {
        await Assert.That(Catch(() => StationPlanner.Plan(10, 10, 5)) is ArgumentException).IsTrue();
        await Assert.That(Catch(() => StationPlanner.Plan(10, 5, 5)) is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Plan_ANonPositiveInterval_StillTerminatesWithAFiniteNumberOfStations()
    {
        var stations = StationPlanner.Plan(0, 10, 0);

        await Assert.That(stations.Count).IsGreaterThan(2);
        await Assert.That(stations.Count).IsLessThanOrEqualTo(10001);
    }

    [Test]
    public async Task Plan_LongerThanOneIntervalIsAlwaysAtLeastTwoStations()
    {
        var stations = StationPlanner.Plan(0, 1, 100);

        await Assert.That(stations.Count).IsEqualTo(2);
    }

    // ---- Refine -------------------------------------------------------------------------------------------------

    [Test]
    public async Task Refine_Null_Throws()
    {
        await Assert.That(Catch(() => StationPlanner.Refine(null, Straight, 0.01, 0.1)) is ArgumentNullException).IsTrue();
        await Assert.That(Catch(() => StationPlanner.Refine(new List<double> { 0, 1 }, null, 0.01, 0.1)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Refine_NoStations_GivesNoStations()
    {
        await Assert.That(StationPlanner.Refine(new List<double>(), Straight, 0.01, 0.1).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Refine_AStraightRun_AddsNothing()
    {
        var stations = new List<double> { 0, 25, 50, 75, 100 };

        var refined = StationPlanner.Refine(stations, Straight, 0.01, 0.1);

        await Assert.That(refined.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Refine_ACurve_AddsStationsUntilEverySpanIsWithinTolerance()
    {
        const double tolerance = 0.05;
        var path = CircleOfRadius(50);
        var stations = new List<double> { 0, 20, 40, 60, 80 };

        var refined = StationPlanner.Refine(stations, path, tolerance, 0.1);

        await Assert.That(refined.Count).IsGreaterThan(stations.Count);
        for (var i = 1; i < refined.Count; i++)
        {
            var a = path(refined[i - 1]);
            var b = path(refined[i]);
            var middle = path((refined[i - 1] + refined[i]) / 2);
            var sag = PathPoint.Distance(middle, new PathPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2));
            await Assert.That(sag).IsLessThanOrEqualTo(tolerance);
        }
    }

    [Test]
    public async Task Refine_KeepsEveryOriginalStation_InOrder()
    {
        var stations = new List<double> { 0, 20, 40, 60 };

        var refined = StationPlanner.Refine(stations, CircleOfRadius(30), 0.02, 0.1);

        foreach (var station in stations)
        {
            await Assert.That(refined.Contains(station)).IsTrue();
        }

        for (var i = 1; i < refined.Count; i++)
        {
            await Assert.That(refined[i]).IsGreaterThan(refined[i - 1]);
        }
    }

    [Test]
    public async Task Refine_NeverSplitsBelowTheMinimumSpacing()
    {
        var refined = StationPlanner.Refine(new List<double> { 0, 40 }, CircleOfRadius(2), 1e-6, 1.0);

        for (var i = 1; i < refined.Count; i++)
        {
            await Assert.That(refined[i] - refined[i - 1]).IsGreaterThanOrEqualTo(1.0 - 1e-9);
        }
    }

    [Test]
    public async Task Refine_StopsAtTheDepthLimit()
    {
        var refined = StationPlanner.Refine(new List<double> { 0, 64 }, CircleOfRadius(3), 1e-9, 1e-9, maxDepth: 2);

        // two halvings of one span at most: 4 pieces, 5 stations
        await Assert.That(refined.Count).IsLessThanOrEqualTo(5);
    }

    [Test]
    public async Task Refine_FindsACrestInTheProfile()
    {
        // a sag in elevation with a straight plan: z = 0.002 (x - 50)^2
        PathPoint Parabola(double x) => new PathPoint(x, 0, 0.002 * (x - 50) * (x - 50));

        var refined = StationPlanner.Refine(new List<double> { 0, 100 }, Parabola, 0.01, 0.5);

        await Assert.That(refined.Count).IsGreaterThan(4);
    }

    [Test]
    public async Task Refine_EvaluatesEachStationOnce()
    {
        var calls = new Dictionary<double, int>();
        PathPoint Counting(double station)
        {
            calls[station] = calls.TryGetValue(station, out var n) ? n + 1 : 1;
            return CircleOfRadius(40)(station);
        }

        StationPlanner.Refine(new List<double> { 0, 20, 40 }, Counting, 0.05, 0.5);

        await Assert.That(calls.Values.Max()).IsEqualTo(1);
    }
}

public class SectionProfileTests : TopoAlignTest
{
    private static PlanPoint P(double u, double v) => new PlanPoint(u, v);

    /// <summary>A 12 ft wide, 6 inch thick slab hanging below the centreline level.</summary>
    private static List<PlanSegment> Slab() => new List<PlanSegment>
    {
        new PlanLine(P(-6, 0), P(6, 0)),
        new PlanLine(P(6, 0), P(6, -0.5)),
        new PlanLine(P(6, -0.5), P(-6, -0.5)),
        new PlanLine(P(-6, -0.5), P(-6, 0))
    };

    private static SectionProfile Make(IReadOnlyList<PlanSegment> segments)
    {
        if (!SectionProfile.TryCreate(segments, out var profile, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return profile;
    }

    // ---- creating -----------------------------------------------------------------------------------------------

    [Test]
    public async Task TryCreate_ASlab_MeasuresWidthHeightAndArea()
    {
        var ok = SectionProfile.TryCreate(Slab(), out var profile, out var error);

        await Assert.That(ok).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(profile.Width).IsEqualTo(12.0).Within(1e-9);
        await Assert.That(profile.Height).IsEqualTo(0.5).Within(1e-9);
        await Assert.That(profile.Area).IsEqualTo(6.0).Within(1e-9);
        await Assert.That(profile.MinU).IsEqualTo(-6.0).Within(1e-9);
        await Assert.That(profile.MaxV).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(profile.Segments.Count).IsEqualTo(4);
    }

    [Test]
    public async Task TryCreate_WithAnArc_UsesTheCurveForTheMeasurements()
    {
        // a semicircular crown of radius 6 on a flat base: area = pi * 36 / 2
        var segments = new List<PlanSegment>
        {
            new PlanLine(P(-6, 0), P(6, 0)),
            new PlanArc(P(0, 0), 6, 0, Math.PI)
        };

        var ok = SectionProfile.TryCreate(segments, out var profile, out _);

        await Assert.That(ok).IsTrue();
        await Assert.That(profile.Height).IsEqualTo(6.0).Within(0.01);
        await Assert.That(profile.Area).IsEqualTo(Math.PI * 18).Within(0.2);
    }

    [Test]
    public async Task TryCreate_TooFewSegments_IsRefused()
    {
        await Assert.That(SectionProfile.TryCreate(null, out _, out var e1)).IsFalse();
        await Assert.That(SectionProfile.TryCreate(new List<PlanSegment>(), out _, out _)).IsFalse();
        await Assert.That(SectionProfile.TryCreate(new List<PlanSegment> { new PlanLine(P(0, 0), P(1, 0)) }, out _, out var e2)).IsFalse();
        await Assert.That(e1.Contains("closed")).IsTrue();
        await Assert.That(e2.Contains("closed")).IsTrue();
    }

    [Test]
    public async Task TryCreate_AnOpenShape_IsRefusedAsNotClosed()
    {
        var open = new List<PlanSegment>
        {
            new PlanLine(P(-6, 0), P(6, 0)),
            new PlanLine(P(6, 0), P(6, -0.5)),
            new PlanLine(P(6, -0.5), P(-6, -0.5))
        };

        var ok = SectionProfile.TryCreate(open, out var profile, out var error);

        await Assert.That(ok).IsFalse();
        await Assert.That(profile).IsNull();
        await Assert.That(error.Contains("closed")).IsTrue();
    }

    [Test]
    public async Task TryCreate_CurvesThatDoNotMeet_AreRefused()
    {
        var gap = new List<PlanSegment>
        {
            new PlanLine(P(-6, 0), P(6, 0)),
            new PlanLine(P(7, 0), P(7, -0.5)),
            new PlanLine(P(7, -0.5), P(-6, -0.5)),
            new PlanLine(P(-6, -0.5), P(-6, 0))
        };

        var ok = SectionProfile.TryCreate(gap, out _, out var error);

        await Assert.That(ok).IsFalse();
        await Assert.That(error.Contains("connected")).IsTrue();
    }

    [Test]
    public async Task TryCreate_AShapeThatEnclosesNothing_IsRefused()
    {
        var flat = new List<PlanSegment>
        {
            new PlanLine(P(0, 0), P(10, 0)),
            new PlanLine(P(10, 0), P(0, 0))
        };

        var ok = SectionProfile.TryCreate(flat, out _, out var error);

        await Assert.That(ok).IsFalse();
        await Assert.That(error.Contains("area")).IsTrue();
    }

    [Test]
    public async Task TryCreate_SpiralsAndTinyCurves_AreRefused()
    {
        var spiral = new List<PlanSegment>
        {
            new PlanSpiral(P(0, 0), 0, 0, 0.1, 5),
            new PlanLine(P(0, 0), P(0, 0.1))
        };
        var tiny = Slab();
        tiny[1] = new PlanLine(P(6, 0), P(6, 0.0000001));

        await Assert.That(SectionProfile.TryCreate(spiral, out _, out var e1)).IsFalse();
        await Assert.That(e1.Contains("straight lines and arcs")).IsTrue();
        await Assert.That(SectionProfile.TryCreate(tiny, out _, out var e2)).IsFalse();
        await Assert.That(e2.Contains("too short")).IsTrue();
    }

    // ---- mirroring ----------------------------------------------------------------------------------------------

    [Test]
    public async Task Mirrored_FlipsLeftToRight_AndKeepsTheSize()
    {
        var offset = new List<PlanSegment>
        {
            new PlanLine(P(0, 0), P(8, 0)),
            new PlanLine(P(8, 0), P(8, -1)),
            new PlanLine(P(8, -1), P(0, -1)),
            new PlanLine(P(0, -1), P(0, 0))
        };
        var profile = Make(offset);

        var mirrored = profile.Mirrored();

        await Assert.That(mirrored.MinU).IsEqualTo(-8.0).Within(1e-9);
        await Assert.That(mirrored.MaxU).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(mirrored.Width).IsEqualTo(profile.Width).Within(1e-9);
        await Assert.That(mirrored.Area).IsEqualTo(profile.Area).Within(1e-9);
    }

    [Test]
    public async Task Mirrored_StaysAClosedLoop_ArcsIncluded()
    {
        var segments = new List<PlanSegment>
        {
            new PlanLine(P(-6, 0), P(6, 0)),
            new PlanArc(P(0, 0), 6, 0, Math.PI)
        };
        var profile = Make(segments);

        var mirrored = profile.Mirrored();

        var ok = SectionProfile.TryCreate(mirrored.Segments, out _, out var error);
        await Assert.That(ok).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(mirrored.Area).IsEqualTo(profile.Area).Within(1e-6);
        await Assert.That(mirrored.Height).IsEqualTo(profile.Height).Within(1e-9);
    }

    [Test]
    public async Task Mirrored_Twice_GivesBackTheOriginalPoints()
    {
        var segments = new List<PlanSegment>
        {
            new PlanLine(P(-3, 0), P(9, 0)),
            new PlanArc(P(3, 0), 6, 0, Math.PI)
        };
        var profile = Make(segments);

        var twice = profile.Mirrored().Mirrored();

        for (var i = 0; i < segments.Count; i++)
        {
            await Assert.That(PlanPoint.Distance(twice.Segments[i].StartPoint, profile.Segments[i].StartPoint)).IsLessThan(1e-9);
            await Assert.That(PlanPoint.Distance(twice.Segments[i].EndPoint, profile.Segments[i].EndPoint)).IsLessThan(1e-9);
        }
    }

    [Test]
    public async Task Outline_ContainsTheVerticesAndSamplesTheArcs()
    {
        var flat = Make(Slab());
        var curved = Make(new List<PlanSegment> { new PlanLine(P(-6, 0), P(6, 0)), new PlanArc(P(0, 0), 6, 0, Math.PI) });

        await Assert.That(flat.Outline().Count).IsEqualTo(4);
        await Assert.That(curved.Outline().Count).IsGreaterThan(10);
    }
}

public class SectionFrameTests : TopoAlignTest
{
    [Test]
    public async Task ToWorld_HeadingEast_PutsPositiveUOnTheSouthSide()
    {
        // to the right of someone walking east is south
        var point = SectionFrame.ToWorld(2, 1, new PathPoint(10, 20, 5), 1, 0, 0);

        await Assert.That(point.X).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(point.Y).IsEqualTo(18.0).Within(1e-9);
        await Assert.That(point.Z).IsEqualTo(6.0).Within(1e-9);
    }

    [Test]
    public async Task ToWorld_HeadingNorth_PutsPositiveUOnTheEastSide()
    {
        var point = SectionFrame.ToWorld(3, -0.5, new PathPoint(10, 20, 5), 0, 1, 0);

        await Assert.That(point.X).IsEqualTo(13.0).Within(1e-9);
        await Assert.That(point.Y).IsEqualTo(20.0).Within(1e-9);
        await Assert.That(point.Z).IsEqualTo(4.5).Within(1e-9);
    }

    [Test]
    public async Task ToWorld_NegativeU_IsToTheLeft()
    {
        var point = SectionFrame.ToWorld(-4, 0, new PathPoint(0, 0, 0), 1, 0, 0);

        await Assert.That(point.Y).IsEqualTo(4.0).Within(1e-9);
    }

    [Test]
    public async Task ToWorld_TheTangentNeedNotBeAUnitVector()
    {
        var point = SectionFrame.ToWorld(2, 0, new PathPoint(0, 0, 0), 0, 50, 0);

        await Assert.That(point.X).IsEqualTo(2.0).Within(1e-9);
        await Assert.That(point.Y).IsEqualTo(0.0).Within(1e-9);
    }

    [Test]
    public async Task ToWorld_Diagonal_IsPerpendicularToTheDirection()
    {
        var point = SectionFrame.ToWorld(10, 0, new PathPoint(0, 0, 0), 1, 1, 0);

        await Assert.That(point.X).IsEqualTo(10 * Math.Sqrt(0.5)).Within(1e-9);
        await Assert.That(point.Y).IsEqualTo(-10 * Math.Sqrt(0.5)).Within(1e-9);
        // along (1,1) the dot product with the offset is zero
        await Assert.That(point.X * 1 + point.Y * 1).IsEqualTo(0.0).Within(1e-9);
    }

    [Test]
    public async Task ToWorld_VerticalOffset_RaisesOnlyTheHeight()
    {
        var plain = SectionFrame.ToWorld(1, 1, new PathPoint(0, 0, 0), 1, 0, 0);
        var raised = SectionFrame.ToWorld(1, 1, new PathPoint(0, 0, 0), 1, 0, 0.25);

        await Assert.That(raised.X).IsEqualTo(plain.X);
        await Assert.That(raised.Y).IsEqualTo(plain.Y);
        await Assert.That(raised.Z - plain.Z).IsEqualTo(0.25).Within(1e-12);
    }

    [Test]
    public async Task ToWorld_NoDirection_Throws()
    {
        var exception = Catch(() => SectionFrame.ToWorld(1, 1, new PathPoint(0, 0, 0), 0, 0, 0));

        await Assert.That(exception is ArgumentException).IsTrue();
    }

    [Test]
    public async Task ToWorld_TheSectionStaysUprightWhateverTheGrade()
    {
        // the section's "up" is world up: the height of a point does not depend on which way the path points
        var heights = new[] { 0.0, 1.0, 2.0, 3.0 }
            .Select(angle => SectionFrame.ToWorld(5, 2, new PathPoint(0, 0, 10), Math.Cos(angle), Math.Sin(angle), 0).Z)
            .ToArray();

        foreach (var height in heights)
        {
            await Assert.That(height).IsEqualTo(12.0).Within(1e-12);
        }
    }
}

public class PathPointTests : TopoAlignTest
{
    [Test]
    public async Task Distance_IsThreeDimensional()
    {
        await Assert.That(PathPoint.Distance(new PathPoint(0, 0, 0), new PathPoint(2, 3, 6))).IsEqualTo(7.0).Within(1e-12);
    }
}
