using TopoAlign.Alignments;
using TopoAlign.Grading;
using static TopoAlign.Tests.TestKit;

namespace TopoAlign.Tests;

/// <summary>Where a slope leaving a line meets the ground, worked out from triangles alone (no Revit geometry involved).</summary>
public class SlopeGraderTests : TopoAlignTest
{
    private const double Tol = 1e-3;

    /// <summary>A square of ground from (-100, -100) to (100, 100) rising by <paramref name="gradientX"/> per foot in X.</summary>
    private static TriangleSurface Ground(double elevation, double gradientX = 0.0)
    {
        double Z(double x) => elevation + gradientX * x;

        var surface = new TriangleSurface();
        surface.AddTriangle(-100, -100, Z(-100), 100, -100, Z(100), 100, 100, Z(100));
        surface.AddTriangle(-100, -100, Z(-100), 100, 100, Z(100), -100, 100, Z(-100));
        return surface;
    }

    /// <summary>A straight line along Y at x = 0, from y = -10 to y = 10, at the given height.</summary>
    private static List<GradePoint> LineAlongY(double z)
    {
        var path = new List<GradePoint>();
        for (var y = -10; y <= 10; y += 5)
        {
            path.Add(new GradePoint(0, y, z));
        }

        return path;
    }

    [Test]
    public async Task OneToThree_FromAnEdgeFiveFeetAboveFlatGround_MeetsItFifteenFeetAway()
    {
        var result = SlopeGrader.Grade(LineAlongY(5.0), false, 50, 0, SlopeUnits.ToRiseOverRun(SlopeMode.Ratio, 3), 100, Ground(0.0), true);

        await Assert.That(result.HitCount).IsEqualTo(result.Stations.Count);
        foreach (var station in result.Stations)
        {
            await Assert.That(station.IsCut).IsFalse();
            await Assert.That(station.Daylight.X).IsEqualTo(15.0).Within(Tol);
            await Assert.That(station.Daylight.Z).IsEqualTo(0.0).Within(Tol);
        }
    }

    [Test]
    public async Task TheSidePicked_DecidesWhichWayTheSlopeRuns()
    {
        var east = SlopeGrader.Grade(LineAlongY(5.0), false, 50, 0, 1.0 / 3.0, 100, Ground(0.0), true);
        var west = SlopeGrader.Grade(LineAlongY(5.0), false, -50, 0, 1.0 / 3.0, 100, Ground(0.0), true);

        await Assert.That(east.Stations[2].Daylight.X).IsEqualTo(15.0).Within(Tol);
        await Assert.That(west.Stations[2].Daylight.X).IsEqualTo(-15.0).Within(Tol);
    }

    [Test]
    public async Task AnEdgeBelowTheGround_CutsUpToIt()
    {
        var result = SlopeGrader.Grade(LineAlongY(-4.0), false, 50, 0, 0.5, 100, Ground(0.0), true);

        await Assert.That(result.CutCount).IsEqualTo(result.Stations.Count);
        await Assert.That(result.Stations[0].Daylight.X).IsEqualTo(8.0).Within(Tol);
        await Assert.That(result.Stations[0].Daylight.Z).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task GroundThatFallsAwayTheWayTheSlopeRuns_TakesLongerToMeet()
    {
        // ground drops 0.1 per foot towards +X: a 1:3 fall from 5 feet up needs d where 5 - d/3 = -0.1 d
        var result = SlopeGrader.Grade(LineAlongY(5.0), false, 50, 0, 1.0 / 3.0, 200, Ground(0.0, -0.1), true);

        var expected = 5.0 / (1.0 / 3.0 - 0.1);
        await Assert.That(result.Stations[0].Daylight.X).IsEqualTo(expected).Within(Tol);
        await Assert.That(result.Stations[0].Daylight.Z).IsEqualTo(-0.1 * expected).Within(Tol);
    }

    [Test]
    public async Task OneLine_CanBeCutAtOneEndAndFillAtTheOther()
    {
        // the ground is level at 0: the line is 3 feet above it at one end, on it in the middle and 3 feet below it at the other
        var path = new List<GradePoint> { new(0, -10, 3.0), new(0, 0, 0.0), new(0, 10, -3.0) };
        var result = SlopeGrader.Grade(path, false, 50, 0, 0.5, 100, Ground(0.0), true);

        await Assert.That(result.Stations[0].IsCut).IsFalse();
        await Assert.That(result.Stations[2].IsCut).IsTrue();
        await Assert.That(result.Stations[0].Daylight.X).IsEqualTo(6.0).Within(Tol);
        await Assert.That(result.Stations[2].Daylight.X).IsEqualTo(6.0).Within(Tol);
    }

    [Test]
    public async Task AStationThatNeverMeetsTheGroundWithinTheMaximumWidth_IsLeftOut()
    {
        var result = SlopeGrader.Grade(LineAlongY(5.0), false, 50, 0, 1.0 / 3.0, 10, Ground(0.0), true);

        await Assert.That(result.HitCount).IsEqualTo(0);
        await Assert.That(result.MissCount).IsEqualTo(result.Stations.Count);
        await Assert.That(result.Corridor.Count).IsEqualTo(0);
        await Assert.That(result.NewPoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ARayThatRunsOffTheSurface_EndsOnTheEdgeAtTheSlopesHeight()
    {
        // line at x = 90, slope 1:3 from 5 feet would need 15 feet but the ground ends at x = 100
        var path = new List<GradePoint> { new(90, -10, 5.0), new(90, 10, 5.0) };
        var result = SlopeGrader.Grade(path, false, 200, 0, 1.0 / 3.0, 100, Ground(0.0), true);

        await Assert.That(result.HitCount).IsEqualTo(2);
        await Assert.That(result.ClippedCount).IsEqualTo(2);
        await Assert.That(result.Corridor.Count).IsEqualTo(1);
        foreach (var station in result.Stations)
        {
            await Assert.That(station.Daylight.X).IsEqualTo(100.0).Within(Tol);
            await Assert.That(station.Daylight.Z).IsEqualTo(5.0 - 10.0 / 3.0).Within(Tol);
        }
    }

    [Test]
    public async Task ASlopeOnTheInsideOfATightBend_DropsDaylightPointsThatWouldBunchUpOrCross()
    {
        // a ring of radius 1 foot graded inwards: the rays all head for the middle and cross
        var path = new List<GradePoint>();
        for (var i = 0; i < 16; i++)
        {
            var angle = i * 2.0 * Math.PI / 16.0;
            path.Add(new GradePoint(Math.Cos(angle), Math.Sin(angle), 5.0));
        }

        var result = SlopeGrader.Grade(path, true, 0, 0, 1.0, 100, Ground(0.0), true);

        await Assert.That(result.Stations.Count(station => station.DaylightSkipped)).IsGreaterThan(0);
        await Assert.That(result.NewPoints.Count).IsLessThan(2 * result.Stations.Count);
    }

    [Test]
    public async Task ASlopeAlongAStraightLine_KeepsEveryDaylightPoint()
    {
        var result = SlopeGrader.Grade(LineAlongY(5.0), false, 50, 0, 1.0 / 3.0, 100, Ground(0.0), true);

        await Assert.That(result.Stations.Count(station => station.DaylightSkipped)).IsEqualTo(0);
        await Assert.That(result.NewPoints.Count).IsEqualTo(2 * result.Stations.Count);
    }

    [Test]
    public async Task ALineAlreadyOnTheEdge_WithTheSlopeRunningOffIt_IsLeftOut()
    {
        // line on the ground's edge at x = 100, with the side picked beyond it
        var path = new List<GradePoint> { new(100, -10, 5.0), new(100, 10, 5.0) };
        var result = SlopeGrader.Grade(path, false, 200, 0, 1.0 / 3.0, 100, Ground(0.0), true);

        await Assert.That(result.HitCount).IsEqualTo(0);
    }

    [Test]
    public async Task TheCorridor_CoversTheSlope_AndNotWhatIsBeyondIt()
    {
        var result = SlopeGrader.Grade(LineAlongY(5.0), false, 50, 0, 1.0 / 3.0, 100, Ground(0.0), true);

        await Assert.That(result.Contains(7.5, 0, 0.01)).IsTrue();
        await Assert.That(result.Contains(14.0, -9, 0.01)).IsTrue();
        await Assert.That(result.Contains(0.0, 0, 0.01)).IsTrue();
        await Assert.That(result.Contains(16.0, 0, 0.01)).IsFalse();
        await Assert.That(result.Contains(-1.0, 0, 0.01)).IsFalse();
        await Assert.That(result.Contains(5.0, 12, 0.01)).IsFalse();
    }

    [Test]
    public async Task NewPoints_AreTheEdgeAndTheDaylight_OrJustTheDaylight()
    {
        var path = LineAlongY(5.0);
        var with = SlopeGrader.Grade(path, false, 50, 0, 1.0 / 3.0, 100, Ground(0.0), true);
        var without = SlopeGrader.Grade(path, false, 50, 0, 1.0 / 3.0, 100, Ground(0.0), false);

        await Assert.That(with.NewPoints.Count).IsEqualTo(path.Count * 2);
        await Assert.That(without.NewPoints.Count).IsEqualTo(path.Count);
        await Assert.That(without.NewPoints.All(point => Math.Abs(point.Z) < Tol)).IsTrue();
    }

    [Test]
    public async Task AClosedLoop_GradesOutwardFromAPointOutsideAndInwardFromOneInside()
    {
        var square = new List<GradePoint>
        {
            new(-10, -10, 3.0), new(0, -10, 3.0), new(10, -10, 3.0), new(10, 0, 3.0),
            new(10, 10, 3.0), new(0, 10, 3.0), new(-10, 10, 3.0), new(-10, 0, 3.0)
        };

        var outward = SlopeGrader.Grade(square, true, 50, 0, 1.0, 100, Ground(0.0), true);
        var inward = SlopeGrader.Grade(square, true, 0, 0, 1.0, 100, Ground(0.0), true);

        // 3 feet of fall at 1:1 is 3 feet of run; the middle of the east side is at index 3
        await Assert.That(outward.Stations[3].Daylight.X).IsEqualTo(13.0).Within(Tol);
        await Assert.That(inward.Stations[3].Daylight.X).IsEqualTo(7.0).Within(Tol);
        await Assert.That(outward.Corridor.Count).IsEqualTo(square.Count);
    }

    [Test]
    public async Task AtACorner_TheSlopeIsStillMeasuredSquareToTheLine()
    {
        // an L-shaped line: the corner at the origin must come out three feet from both legs for a 3 foot drop at 1:1
        var path = new List<GradePoint> { new(-10, 0, 3.0), new(0, 0, 3.0), new(0, -10, 3.0) };
        var result = SlopeGrader.Grade(path, false, -5, -5, 1.0, 100, Ground(0.0), true);

        // the side picked is the inside of the L, so the corner's ray runs towards (-1, -1)
        var corner = result.Stations[1];
        await Assert.That(corner.Daylight.X).IsEqualTo(-3.0).Within(Tol);
        await Assert.That(corner.Daylight.Y).IsEqualTo(-3.0).Within(Tol);
    }

    [Test]
    public async Task SlopeUnits_ConvertToTheSameRiseOverRun()
    {
        await Assert.That(SlopeUnits.ToRiseOverRun(SlopeMode.Ratio, 3)).IsEqualTo(1.0 / 3.0).Within(1e-9);
        await Assert.That(SlopeUnits.ToRiseOverRun(SlopeMode.Percent, 100.0 / 3.0)).IsEqualTo(1.0 / 3.0).Within(1e-9);
        await Assert.That(SlopeUnits.ToRiseOverRun(SlopeMode.Degrees, 45)).IsEqualTo(1.0).Within(1e-9);
        await Assert.That(SlopeUnits.ToRiseOverRun(SlopeMode.Degrees, 18.434948822922)).IsEqualTo(1.0 / 3.0).Within(1e-9);
    }

    [Test]
    public async Task SlopeUnits_RejectValuesThatAreNotASlope()
    {
        var zero = Catch(() => SlopeUnits.ToRiseOverRun(SlopeMode.Ratio, 0));
        var negative = Catch(() => SlopeUnits.ToRiseOverRun(SlopeMode.Percent, -5));
        var vertical = Catch(() => SlopeUnits.ToRiseOverRun(SlopeMode.Degrees, 90));

        await Assert.That(zero is ArgumentOutOfRangeException).IsTrue();
        await Assert.That(negative is ArgumentOutOfRangeException).IsTrue();
        await Assert.That(vertical is ArgumentOutOfRangeException).IsTrue();
    }
}
