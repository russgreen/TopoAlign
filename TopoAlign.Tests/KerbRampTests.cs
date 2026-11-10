using TopoAlign.Alignments;
using TopoAlign.Grading;
using TopoAlign.Geometry.Topography;
using TopoAlign.Grading.Kerb;
using TopoAlign.Grading.TopoEdge;
using static TopoAlign.Tests.TestKit;

namespace TopoAlign.Tests;

/// <summary>The ramp and flares of a dropped kerb, worked out from outlines and triangles alone (no Revit geometry involved).</summary>
public class KerbRampTests : TopoAlignTest
{
    private const double Tol = 1e-3;

    private static GradePoint P(double x, double y, double z = 0.0) => new(x, y, z);

    private static List<EdgeSegment> Outline(params GradePoint[] ring)
    {
        var segments = new List<EdgeSegment>();
        for (var i = 0; i < ring.Length; i++)
        {
            segments.Add(new EdgeSegment(ring[i], ring[(i + 1) % ring.Length]));
        }

        return segments;
    }

    /// <summary>A footway at the given level, x from 0 to 100 and y from -50 to 50, with its kerb along x = 0.</summary>
    private static TriangleSurface Footway(double z)
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(0, -50, z, 100, -50, z, 100, 50, z);
        surface.AddTriangle(0, -50, z, 100, 50, z, 0, 50, z);
        return surface;
    }

    private static List<EdgeSegment> FootwayOutline(double z) =>
        Outline(P(0, -50, z), P(100, -50, z), P(100, 50, z), P(0, 50, z));

    /// <summary>The road, x from -100 to 0, whose east edge is the kerb. Its level rises by <paramref name="slope"/> per foot of y.</summary>
    private static List<EdgeSegment> RoadOutline(double z, double slope = 0.0) =>
        Outline(P(-100, -50, z - 50 * slope), P(0, -50, z - 50 * slope), P(0, 50, z + 50 * slope), P(-100, 50, z + 50 * slope));

    private static KerbRampInput Input(
        IReadOnlyList<EdgeSegment> footway,
        TriangleSurface surface,
        IReadOnlyList<EdgeSegment> road,
        double centreY = 0.0,
        double width = 4.0,
        double maxLength = 20.0,
        double offset = 0.0) =>
        new(footway, 0, 0, road, 0, 0, surface, 0, centreY, width, 12, 12, maxLength, offset);

    [Test]
    public async Task HalfAFootDrop_AtOneInTwelve_RampsUpOverSixFeet()
    {
        var result = KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0)));

        await Assert.That(result.Stations).IsEqualTo(5);
        await Assert.That(result.RampLength).IsEqualTo(6.0).Within(Tol);

        // the kerb end of the ramp is at the road's level, and the top is back at the footway's
        var kerb = result.NewPoints.Where(p => Math.Abs(p.X) < 1e-6 && Math.Abs(p.Y) <= 2.0 + 1e-6).ToList();
        await Assert.That(kerb.Count).IsEqualTo(5);
        foreach (var point in kerb)
        {
            await Assert.That(point.Z).IsEqualTo(0.0).Within(Tol);
        }

        var top = result.NewPoints.Where(p => Math.Abs(p.X - 6.0) < Tol).ToList();
        await Assert.That(top.Count).IsEqualTo(5);
        foreach (var point in top)
        {
            await Assert.That(point.Z).IsEqualTo(0.5).Within(Tol);
        }
    }

    [Test]
    public async Task EachFlare_RunsAlongTheKerbUntilItIsBackAtTheKerbsLevel()
    {
        var result = KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0)));

        await Assert.That(result.StartFlareLength).IsEqualTo(6.0).Within(Tol);
        await Assert.That(result.EndFlareLength).IsEqualTo(6.0).Within(Tol);
        await Assert.That(result.Warnings.Count).IsEqualTo(0);

        // the flares end on the kerb at the footway's level, either side of the 4 ft drop
        await Assert.That(result.NewPoints.Any(p => Math.Abs(p.X) < 1e-6 && Math.Abs(p.Y - 8.0) < Tol && Math.Abs(p.Z - 0.5) < Tol)).IsTrue();
        await Assert.That(result.NewPoints.Any(p => Math.Abs(p.X) < 1e-6 && Math.Abs(p.Y + 8.0) < Tol && Math.Abs(p.Z - 0.5) < Tol)).IsTrue();

        // 5 stations (kerb and top each) and the two flare ends
        await Assert.That(result.NewPoints.Count).IsEqualTo(12);
        await Assert.That(result.ClearPolygons.Count).IsEqualTo(4 + 2);
    }

    [Test]
    public async Task ExistingPointsInsideTheRampOrFlares_AreClaimedForReplacement()
    {
        var result = KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0)));

        await Assert.That(result.Contains(3.0, 0.0, 1e-3)).IsTrue();
        await Assert.That(result.Contains(0.0, 6.0, 1e-3)).IsTrue();
        await Assert.That(result.Contains(1.0, -4.0, 1e-3)).IsTrue();

        // beyond the flare, past the top of the ramp, and well inside the footway
        await Assert.That(result.Contains(0.0, 9.0, 1e-3)).IsFalse();
        await Assert.That(result.Contains(7.0, 0.0, 1e-3)).IsFalse();
        await Assert.That(result.Contains(50.0, 20.0, 1e-3)).IsFalse();
    }

    [Test]
    public async Task ASlopingRoadEdge_SetsTheKerbLevelAlongTheRamp()
    {
        // the road edge is 0.02 higher for every 2 ft along y
        var result = KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0, 0.01)));

        var kerbPoint = result.NewPoints.First(p => Math.Abs(p.X) < 1e-6 && Math.Abs(p.Y - 2.0) < Tol);
        await Assert.That(kerbPoint.Z).IsEqualTo(0.02).Within(Tol);

        // a higher kerb level means a shorter ramp up to the footway
        var top = result.NewPoints.First(p => p.X > 1.0 && Math.Abs(p.Y - 2.0) < 0.5);
        await Assert.That(top.X).IsEqualTo((0.5 - 0.02) * 12.0).Within(Tol);
    }

    [Test]
    public async Task ALevelOffset_RaisesTheLevelTheRampDropsTo()
    {
        var result = KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0), offset: 0.25));

        await Assert.That(result.RampLength).IsEqualTo(3.0).Within(Tol);
        await Assert.That(result.StartFlareLength).IsEqualTo(3.0).Within(Tol);
    }

    [Test]
    public async Task ARoadHigherThanTheFootway_RampsDownAndHasNoFlare()
    {
        var result = KerbRamp.Build(Input(FootwayOutline(0.0), Footway(0.0), RoadOutline(0.5)));

        await Assert.That(result.StartFlareLength).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.EndFlareLength).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.ClearPolygons.Count).IsEqualTo(4);
    }

    [Test]
    public async Task AVertexOnTheKerbInsideTheWindow_IsAStation()
    {
        // an extra corner point on the kerb at (0, 0), straight on, which has to be a station
        var footway = Outline(P(0, -50, 0.5), P(100, -50, 0.5), P(100, 50, 0.5), P(0, 50, 0.5), P(0, 0, 0.5));
        var result = KerbRamp.Build(Input(footway, Footway(0.5), RoadOutline(0.0), centreY: 0.5, width: 4.0));

        // the window runs y = -1.5 to 2.5, with a corner at 0: 1 ft spacing gives -1.5, -0.5 (two pieces of 0.75 each side)
        var kerb = result.NewPoints.Where(p => Math.Abs(p.X) < 1e-6 && p.Y >= -1.5 - Tol && p.Y <= 2.5 + Tol).ToList();
        await Assert.That(kerb.Any(p => Math.Abs(p.Y) < Tol)).IsTrue();
        await Assert.That(kerb.Any(p => Math.Abs(p.Y + 1.5) < Tol)).IsTrue();
        await Assert.That(kerb.Any(p => Math.Abs(p.Y - 2.5) < Tol)).IsTrue();
    }

    [Test]
    public async Task ARampThatCannotMeetTheFootwayInTime_IsRefused()
    {
        var exception = Catch(() => KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0), maxLength: 3.0)));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task ASharpCornerInsideTheDroppedSection_IsRefused()
    {
        // the centre is a foot from the footway's corner at (0, 50)
        var exception = Catch(() => KerbRamp.Build(Input(FootwayOutline(0.5), Footway(0.5), RoadOutline(0.0), centreY: 49.0)));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task AnEdgeThatIsNotOnTheSurface_IsRefused()
    {
        // the surface the ramp is cut into is the other side of the kerb to the footway outline
        var wrongSurface = new TriangleSurface();
        wrongSurface.AddTriangle(-100, -50, 0.5, -10, -50, 0.5, -10, 50, 0.5);
        wrongSurface.AddTriangle(-100, -50, 0.5, -10, 50, 0.5, -100, 50, 0.5);

        var exception = Catch(() => KerbRamp.Build(Input(FootwayOutline(0.5), wrongSurface, RoadOutline(0.0))));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task TheFlareStopsAtTheEndOfAnOpenKerb_WithAWarning()
    {
        // a footway whose outline is only the kerb itself, 20 ft long: the flares can't get their full 6 ft either side of a 12 ft drop
        var kerbOnly = new List<EdgeSegment> { new(P(0, -10, 0.5), P(0, 10, 0.5)) };
        var input = new KerbRampInput(kerbOnly, 0, 0, RoadOutline(0.0), 0, 0, Footway(0.5), 0, 0, 12.0, 12, 12, 20, 0);

        var result = KerbRamp.Build(input);

        await Assert.That(result.StartFlareLength).IsEqualTo(4.0).Within(Tol);
        await Assert.That(result.Warnings.Count).IsEqualTo(2);
    }

    [Test]
    public async Task KerbPath_FollowsAClosedOutlineAndWrapsRoundIt()
    {
        var path = KerbPath.FromSegments(FootwayOutline(0.5), 0, 0);

        await Assert.That(path.IsClosed).IsTrue();
        await Assert.That(path.Length).IsEqualTo(400.0).Within(Tol);

        path.Project(0, 20, out var position, out var distance);
        await Assert.That(distance).IsEqualTo(0.0).Within(Tol);
        await Assert.That(path.PointAt(position + path.Length).Y).IsEqualTo(20.0).Within(Tol);
    }

    [Test]
    public async Task KerbPath_InterpolatesHeightsAlongASegment()
    {
        var path = KerbPath.FromSegments(RoadOutline(0.0, 0.01), 0, 0);

        path.Project(0, 25, out var position, out _);
        await Assert.That(path.PointAt(position).Z).IsEqualTo(0.25).Within(Tol);
    }
}
