using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Alignments;

namespace TopoAlign.Tests;

public class PlanSegmentTests : TopoAlignTest
{
    private const double Tol = 1e-9;

    // ---- lines --------------------------------------------------------------------------------------------------

    [Test]
    public async Task Line_HasLengthEndsAndHeading()
    {
        var line = new PlanLine(new PlanPoint(1, 2), new PlanPoint(4, 6));

        await Assert.That(line.Length).IsEqualTo(5.0).Within(Tol);
        await Assert.That(line.StartPoint.X).IsEqualTo(1.0);
        await Assert.That(line.EndPoint.Y).IsEqualTo(6.0);
        await Assert.That(line.HeadingAt(0)).IsEqualTo(Math.Atan2(4, 3)).Within(Tol);
    }

    [Test]
    public async Task Line_PointAt_InterpolatesAndClamps()
    {
        var line = new PlanLine(new PlanPoint(0, 0), new PlanPoint(10, 0));

        await Assert.That(line.PointAt(2.5).X).IsEqualTo(2.5).Within(Tol);
        await Assert.That(line.PointAt(-5).X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(line.PointAt(50).X).IsEqualTo(10.0).Within(Tol);
    }

    [Test]
    public async Task Line_ZeroLength_DoesNotDivideByZero()
    {
        var line = new PlanLine(new PlanPoint(3, 3), new PlanPoint(3, 3));

        await Assert.That(line.Length).IsEqualTo(0.0);
        await Assert.That(line.PointAt(1).X).IsEqualTo(3.0);
    }

    [Test]
    public async Task Line_Reversed_SwapsEnds()
    {
        var reversed = new PlanLine(new PlanPoint(0, 0), new PlanPoint(10, 5)).Reversed();

        await Assert.That(reversed.StartPoint.X).IsEqualTo(10.0);
        await Assert.That(reversed.EndPoint.Y).IsEqualTo(0.0);
        await Assert.That(reversed.Length).IsEqualTo(Math.Sqrt(125)).Within(Tol);
    }

    // ---- arcs ---------------------------------------------------------------------------------------------------

    [Test]
    public async Task Arc_NonPositiveRadius_Throws()
    {
        await Assert.That(Catch(() => new PlanArc(new PlanPoint(0, 0), 0, 0, 1)) is ArgumentOutOfRangeException).IsTrue();
        await Assert.That(Catch(() => new PlanArc(new PlanPoint(0, 0), -3, 0, 1)) is ArgumentOutOfRangeException).IsTrue();
    }

    [Test]
    public async Task Arc_CounterClockwiseQuarter_GoesFromEastToNorth()
    {
        var arc = new PlanArc(new PlanPoint(0, 0), 10, 0, Math.PI / 2);

        await Assert.That(arc.Length).IsEqualTo(Math.PI * 5).Within(Tol);
        await Assert.That(arc.StartPoint.X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(arc.EndPoint.Y).IsEqualTo(10.0).Within(Tol);
        await Assert.That(arc.IsClockwise).IsFalse();
        // heading at the start is north (a left turn), at the end west
        await Assert.That(arc.HeadingAt(0)).IsEqualTo(Math.PI / 2).Within(Tol);
        await Assert.That(arc.HeadingAt(arc.Length)).IsEqualTo(Math.PI).Within(Tol);
    }

    [Test]
    public async Task Arc_ClockwiseQuarter_GoesFromEastToSouth()
    {
        var arc = new PlanArc(new PlanPoint(0, 0), 10, 0, -Math.PI / 2);

        await Assert.That(arc.IsClockwise).IsTrue();
        await Assert.That(arc.EndPoint.Y).IsEqualTo(-10.0).Within(Tol);
        await Assert.That(arc.HeadingAt(0)).IsEqualTo(-Math.PI / 2).Within(Tol);
    }

    [Test]
    public async Task Arc_PointAt_MidpointIsOnTheCircle()
    {
        var arc = new PlanArc(new PlanPoint(5, 5), 10, 0, Math.PI);

        var mid = arc.PointAt(arc.Length / 2);

        await Assert.That(mid.X).IsEqualTo(5.0).Within(1e-9);
        await Assert.That(mid.Y).IsEqualTo(15.0).Within(1e-9);
    }

    [Test]
    public async Task Arc_Reversed_RunsTheOtherWay()
    {
        var arc = new PlanArc(new PlanPoint(0, 0), 10, 0, Math.PI / 2);

        var reversed = (PlanArc)arc.Reversed();

        await Assert.That(reversed.IsClockwise).IsTrue();
        await Assert.That(reversed.StartPoint.Y).IsEqualTo(10.0).Within(Tol);
        await Assert.That(reversed.EndPoint.X).IsEqualTo(10.0).Within(Tol);
        await Assert.That(reversed.Length).IsEqualTo(arc.Length).Within(Tol);
    }

    // ---- spirals ------------------------------------------------------------------------------------------------

    [Test]
    public async Task Spiral_NonPositiveLength_Throws()
    {
        await Assert.That(Catch(() => new PlanSpiral(new PlanPoint(0, 0), 0, 0, 0.01, 0)) is ArgumentOutOfRangeException).IsTrue();
    }

    [Test]
    public async Task Spiral_WithZeroCurvature_IsAStraightLine()
    {
        var spiral = new PlanSpiral(new PlanPoint(0, 0), Math.PI / 4, 0, 0, 100);

        await Assert.That(spiral.EndPoint.X).IsEqualTo(100 * Math.Cos(Math.PI / 4)).Within(1e-9);
        await Assert.That(spiral.EndPoint.Y).IsEqualTo(100 * Math.Sin(Math.PI / 4)).Within(1e-9);
    }

    [Test]
    public async Task Spiral_EndHeadingIsHalfTheCurvatureTimesTheLength()
    {
        const double radius = 200.0, length = 60.0;
        var spiral = new PlanSpiral(new PlanPoint(0, 0), 0, 0, 1.0 / radius, length);

        await Assert.That(spiral.HeadingAt(length)).IsEqualTo(length / (2 * radius)).Within(1e-12);
        await Assert.That(spiral.HeadingAt(0)).IsEqualTo(0.0);
    }

    [Test]
    public async Task Spiral_EndPoint_MatchesTheClothoidSeries()
    {
        const double radius = 200.0, length = 60.0;
        var theta = length / (2 * radius);
        var expectedX = length * (1 - Math.Pow(theta, 2) / 10 + Math.Pow(theta, 4) / 216 - Math.Pow(theta, 6) / 9360);
        var expectedY = length * (theta / 3 - Math.Pow(theta, 3) / 42 + Math.Pow(theta, 5) / 1320 - Math.Pow(theta, 7) / 75600);
        var spiral = new PlanSpiral(new PlanPoint(0, 0), 0, 0, 1.0 / radius, length);

        await Assert.That(spiral.EndPoint.X).IsEqualTo(expectedX).Within(1e-6);
        await Assert.That(spiral.EndPoint.Y).IsEqualTo(expectedY).Within(1e-6);
    }

    [Test]
    public async Task Spiral_RightHandCurvature_CurvesToTheRight()
    {
        var spiral = new PlanSpiral(new PlanPoint(0, 0), 0, 0, -1.0 / 200, 60);

        await Assert.That(spiral.EndPoint.Y).IsLessThan(0.0);
        await Assert.That(spiral.HeadingAt(60)).IsLessThan(0.0);
    }

    [Test]
    public async Task Spiral_Reversed_StartsWhereItEndedAndFacesBack()
    {
        var spiral = new PlanSpiral(new PlanPoint(10, 20), 0.3, 0, 1.0 / 150, 80);

        var reversed = (PlanSpiral)spiral.Reversed();

        await Assert.That(reversed.StartPoint.X).IsEqualTo(spiral.EndPoint.X).Within(1e-9);
        await Assert.That(reversed.StartPoint.Y).IsEqualTo(spiral.EndPoint.Y).Within(1e-9);
        await Assert.That(reversed.EndPoint.X).IsEqualTo(10.0).Within(1e-6);
        await Assert.That(reversed.EndPoint.Y).IsEqualTo(20.0).Within(1e-6);
        await Assert.That(Math.Abs(PiGeometry.NormalizeAngle(reversed.HeadingAt(0) - (spiral.HeadingAt(80) + Math.PI)))).IsLessThan(1e-12);
        await Assert.That(reversed.StartCurvature).IsEqualTo(-spiral.EndCurvature).Within(1e-15);
    }

    [Test]
    public async Task Spiral_Tessellate_IncludesBothEndsAndStaysWithinTolerance()
    {
        var spiral = new PlanSpiral(new PlanPoint(0, 0), 0, 0, 1.0 / 100, 100);
        const double tolerance = 0.005;

        var points = spiral.Tessellate(tolerance);

        await Assert.That(points[0].X).IsEqualTo(0.0);
        await Assert.That(PlanPoint.Distance(points[points.Count - 1], spiral.EndPoint)).IsLessThan(1e-9);
        await Assert.That(points.Count).IsGreaterThanOrEqualTo(5);

        // the middle of every chord must be within the tolerance of the real curve
        var worst = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var d0 = spiral.Length * (i - 1) / (points.Count - 1);
            var d1 = spiral.Length * i / (points.Count - 1);
            var chordMid = new PlanPoint((points[i - 1].X + points[i].X) / 2, (points[i - 1].Y + points[i].Y) / 2);
            var curveMid = spiral.PointAt((d0 + d1) / 2);
            worst = Math.Max(worst, PlanPoint.Distance(chordMid, curveMid));
        }

        await Assert.That(worst).IsLessThanOrEqualTo(tolerance);
    }

    [Test]
    public async Task Spiral_Tessellate_StraightSpiralStillGivesAFewPieces()
    {
        var points = new PlanSpiral(new PlanPoint(0, 0), 0, 0, 0, 40).Tessellate(0.005);

        await Assert.That(points.Count).IsGreaterThanOrEqualTo(5);
    }

    // ---- chains -------------------------------------------------------------------------------------------------

    [Test]
    public async Task ReverseChain_ReversesOrderAndDirection()
    {
        var chain = new PlanSegment[]
        {
            new PlanLine(new PlanPoint(0, 0), new PlanPoint(10, 0)),
            new PlanArc(new PlanPoint(10, 5), 5, -Math.PI / 2, Math.PI / 2),
            new PlanLine(new PlanPoint(15, 5), new PlanPoint(15, 20))
        };

        var reversed = PlanSegment.ReverseChain(chain);

        await Assert.That(reversed.Count).IsEqualTo(3);
        await Assert.That(reversed[0].StartPoint.Y).IsEqualTo(20.0).Within(Tol);
        await Assert.That(reversed[2].EndPoint.X).IsEqualTo(0.0).Within(Tol);
        for (var i = 1; i < reversed.Count; i++)
        {
            await Assert.That(PlanPoint.Distance(reversed[i - 1].EndPoint, reversed[i].StartPoint)).IsLessThan(1e-9);
        }
    }

    [Test]
    public async Task TotalLength_SumsTheSegments()
    {
        var chain = new PlanSegment[]
        {
            new PlanLine(new PlanPoint(0, 0), new PlanPoint(10, 0)),
            new PlanArc(new PlanPoint(10, 5), 5, -Math.PI / 2, Math.PI)
        };

        await Assert.That(PlanSegment.TotalLength(chain)).IsEqualTo(10 + 5 * Math.PI).Within(Tol);
    }
}

public class PiGeometryTests : TopoAlignTest
{
    private static PiPoint P(double x, double y, double radius = 0, double spiral = 0) => new PiPoint(x, y, radius, spiral);

    private static double HeadingAtEnd(PlanSegment s) => s.HeadingAt(s.Length);

    private static async Task AssertContinuous(IReadOnlyList<PlanSegment> segments)
    {
        for (var i = 1; i < segments.Count; i++)
        {
            await Assert.That(PlanPoint.Distance(segments[i - 1].EndPoint, segments[i].StartPoint)).IsLessThan(1e-6);
            await Assert.That(Math.Abs(PiGeometry.NormalizeAngle(HeadingAtEnd(segments[i - 1]) - segments[i].HeadingAt(0)))).IsLessThan(1e-6);
        }
    }

    // ---- laying out ---------------------------------------------------------------------------------------------

    [Test]
    public async Task Create_TwoPoints_IsOneStraight()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(100, 0) });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Segments.Count).IsEqualTo(1);
        await Assert.That(result.Segments[0] is PlanLine).IsTrue();
        await Assert.That(result.Segments[0].Length).IsEqualTo(100.0).Within(1e-9);
    }

    [Test]
    public async Task Create_ZeroRadius_LeavesASharpCorner()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(100, 0, 0), P(100, 100) });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Segments.Count).IsEqualTo(2);
        await Assert.That(result.Segments.All(s => s is PlanLine)).IsTrue();
    }

    [Test]
    public async Task Create_CollinearPoint_IsPassedThrough()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(50, 0, 30, 10), P(100, 0) });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Segments.All(s => s is PlanLine)).IsTrue();
        await Assert.That(PlanSegment.TotalLength(result.Segments)).IsEqualTo(100.0).Within(1e-9);
    }

    [Test]
    public async Task Create_SimpleCurve_IsLineArcLineWithTangentLengthRTanHalfDelta()
    {
        // 90 degree left turn with radius 50: tangent length 50
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 50), P(500, 500) });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Segments.Count).IsEqualTo(3);
        var arc = (PlanArc)result.Segments[1];
        await Assert.That(arc.Radius).IsEqualTo(50.0).Within(1e-9);
        await Assert.That(arc.Sweep).IsEqualTo(Math.PI / 2).Within(1e-9);
        await Assert.That(result.Segments[0].Length).IsEqualTo(450.0).Within(1e-6);
        await Assert.That(result.Segments[2].Length).IsEqualTo(450.0).Within(1e-6);
        await AssertContinuous(result.Segments);
    }

    [Test]
    public async Task Create_RightTurn_GivesAClockwiseArc()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 50), P(500, -500) });

        var arc = (PlanArc)result.Segments[1];
        await Assert.That(arc.IsClockwise).IsTrue();
        await Assert.That(arc.Sweep).IsEqualTo(-Math.PI / 2).Within(1e-9);
        await AssertContinuous(result.Segments);
    }

    [Test]
    public async Task Create_ObtuseDeflection_StillClosesOntoTheNextLine()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(300, 0, 80), P(100, 250) });

        await Assert.That(result.IsValid).IsTrue();
        await AssertContinuous(result.Segments);
        await Assert.That(PlanPoint.Distance(result.Segments[result.Segments.Count - 1].EndPoint, new PlanPoint(100, 250))).IsLessThan(1e-6);
    }

    [Test]
    public async Task Create_WithSpirals_IsLineSpiralArcSpiralLine()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300, 80), P(900, 400) });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Segments.Select(s => s.GetType()).ToArray())
            .IsEquivalentTo(new[] { typeof(PlanLine), typeof(PlanSpiral), typeof(PlanArc), typeof(PlanSpiral), typeof(PlanLine) });
        await AssertContinuous(result.Segments);
    }

    [Test]
    public async Task Create_WithSpirals_ArcSweepIsTheTurnLessTwoSpiralAngles()
    {
        const double radius = 300, spiral = 80;
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, radius, spiral), P(900, 400) });

        var arc = result.Segments.OfType<PlanArc>().Single();
        var expected = Math.PI / 4 - spiral / radius; // a 45 degree turn minus 2 x L/(2R)
        await Assert.That(arc.Sweep).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task Create_WithSpirals_SpiralsStartAtZeroCurvatureAndEndAtTheArcCurvature()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300, 80), P(900, 400) });

        var spirals = result.Segments.OfType<PlanSpiral>().ToList();
        await Assert.That(spirals[0].StartCurvature).IsEqualTo(0.0);
        await Assert.That(spirals[0].EndCurvature).IsEqualTo(1.0 / 300).Within(1e-12);
        await Assert.That(spirals[1].StartCurvature).IsEqualTo(1.0 / 300).Within(1e-12);
        await Assert.That(spirals[1].EndCurvature).IsEqualTo(0.0);
    }

    [Test]
    public async Task Create_WithSpirals_RightTurnUsesNegativeCurvature()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300, 80), P(900, -400) });

        var entry = result.Segments.OfType<PlanSpiral>().First();
        await Assert.That(entry.EndCurvature).IsEqualTo(-1.0 / 300).Within(1e-12);
        await AssertContinuous(result.Segments);
    }

    [Test]
    public async Task Create_EndsExactlyAtTheLastPoint()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300, 80), P(900, 400, 250, 60), P(1100, 1000) });

        await Assert.That(result.IsValid).IsTrue();
        var last = result.Segments[result.Segments.Count - 1].EndPoint;
        await Assert.That(last.X).IsEqualTo(1100.0).Within(1e-6);
        await Assert.That(last.Y).IsEqualTo(1000.0).Within(1e-6);
        await Assert.That(result.Segments[0].StartPoint.X).IsEqualTo(0.0);
        await AssertContinuous(result.Segments);
    }

    [Test]
    public async Task Create_TwoCurvesInSequence_BothFit()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300, 80), P(900, -400, 250, 60), P(1500, -400) });

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Segments.OfType<PlanArc>().Count()).IsEqualTo(2);
        // the first bend heads off to the right (south-east), the second turns back to the left
        await Assert.That(result.Segments.OfType<PlanArc>().First().IsClockwise).IsTrue();
        await Assert.That(result.Segments.OfType<PlanArc>().Last().IsClockwise).IsFalse();
    }

    // ---- errors -------------------------------------------------------------------------------------------------

    [Test]
    public async Task Create_NullOrSinglePoint_IsRejected()
    {
        await Assert.That(PiGeometry.Create(null).IsValid).IsFalse();
        await Assert.That(PiGeometry.Create(new[] { P(0, 0) }).IsValid).IsFalse();
    }

    [Test]
    public async Task Create_DuplicatePoints_AreRejected()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(0, 0), P(10, 0) });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].Contains("same place")).IsTrue();
    }

    [Test]
    public async Task Create_DoublingBack_IsRejected()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(100, 0, 50), P(0, 0.0001) });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("doubles back"))).IsTrue();
    }

    [Test]
    public async Task Create_SpiralsTooLongForTheTurn_AreRejected()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 100, 400), P(600, 100) });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors[0].Contains("too long")).IsTrue();
        await Assert.That(result.Errors[0].Contains("Point 2")).IsTrue();
    }

    [Test]
    public async Task Create_CurvesThatOverlap_AreRejected()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(100, 0, 300), P(200, 100, 300), P(300, 100) });

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(e => e.Contains("overlap"))).IsTrue();
    }

    [Test]
    public async Task Create_CurveTooBigForTheShortFirstLeg_IsRejected()
    {
        var result = PiGeometry.Create(new[] { P(0, 0), P(10, 0, 100), P(10, 100) });

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task NormalizeAngle_WrapsIntoMinusPiToPi()
    {
        await Assert.That(PiGeometry.NormalizeAngle(3 * Math.PI)).IsEqualTo(Math.PI).Within(1e-12);
        await Assert.That(PiGeometry.NormalizeAngle(-3 * Math.PI / 2)).IsEqualTo(Math.PI / 2).Within(1e-12);
        await Assert.That(PiGeometry.NormalizeAngle(0.5)).IsEqualTo(0.5);
    }

    // ---- recovering PIs from picked geometry --------------------------------------------------------------------

    [Test]
    public async Task FromSegments_RecoversThePisOfALineArcLineChain()
    {
        var chain = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300), P(800, 300) }).Segments;

        var pis = PiGeometry.FromSegments(chain, 0, out var reason);

        await Assert.That(reason).IsNull();
        await Assert.That(pis.Count).IsEqualTo(3);
        await Assert.That(pis[1].X).IsEqualTo(500.0).Within(1e-6);
        await Assert.That(pis[1].Y).IsEqualTo(0.0).Within(1e-6);
        await Assert.That(pis[1].Radius).IsEqualTo(300.0).Within(1e-9);
        await Assert.That(pis[2].X).IsEqualTo(800.0).Within(1e-6);
    }

    [Test]
    public async Task FromSegments_KeepsSharpCornersAsZeroRadiusPis()
    {
        var chain = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300), P(800, 300), P(800, 700) }).Segments;

        var pis = PiGeometry.FromSegments(chain, 0, out _);

        await Assert.That(pis.Count).IsEqualTo(4);
        await Assert.That(pis[2].Radius).IsEqualTo(0.0);
        await Assert.That(pis[2].X).IsEqualTo(800.0).Within(1e-6);
    }

    [Test]
    public async Task FromSegments_AddsTheRequestedSpiralLengthToEveryCurve()
    {
        var chain = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300), P(900, 400, 250) , P(1100, 1000) }).Segments;

        var pis = PiGeometry.FromSegments(chain, 70, out _);

        await Assert.That(pis[1].SpiralLength).IsEqualTo(70.0);
        await Assert.That(pis[2].SpiralLength).IsEqualTo(70.0);
        await Assert.That(pis[0].SpiralLength).IsEqualTo(0.0);
        await Assert.That(PiGeometry.Create(pis).IsValid).IsTrue();
    }

    [Test]
    public async Task FromSegments_SpiralledGeometryKeepsTheSamePisAndEnds()
    {
        var chain = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300), P(800, 300) }).Segments;
        var pis = PiGeometry.FromSegments(chain, 60, out _);

        var spiralled = PiGeometry.Create(pis).Segments;

        await Assert.That(PlanPoint.Distance(spiralled[0].StartPoint, chain[0].StartPoint)).IsLessThan(1e-6);
        await Assert.That(PlanPoint.Distance(spiralled[spiralled.Count - 1].EndPoint, chain[chain.Count - 1].EndPoint)).IsLessThan(1e-6);
        await Assert.That(spiralled.OfType<PlanSpiral>().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task FromSegments_NothingToWorkWith_ExplainsWhy()
    {
        await Assert.That(PiGeometry.FromSegments(null, 10, out var reason)).IsNull();
        await Assert.That(reason).IsNotNull();
        await Assert.That(PiGeometry.FromSegments(new List<PlanSegment>(), 10, out reason)).IsNull();
    }

    [Test]
    public async Task FromSegments_ChainThatStartsWithAnArc_IsRefused()
    {
        var chain = new PlanSegment[]
        {
            new PlanArc(new PlanPoint(0, 50), 50, -Math.PI / 2, Math.PI / 2),
            new PlanLine(new PlanPoint(50, 50), new PlanPoint(50, 150))
        };

        await Assert.That(PiGeometry.FromSegments(chain, 10, out var reason)).IsNull();
        await Assert.That(reason.Contains("straight")).IsTrue();
    }

    [Test]
    public async Task FromSegments_TwoArcsInARow_AreRefused()
    {
        var chain = new PlanSegment[]
        {
            new PlanLine(new PlanPoint(-50, 0), new PlanPoint(0, 0)),
            new PlanArc(new PlanPoint(0, 50), 50, -Math.PI / 2, Math.PI / 4),
            new PlanArc(new PlanPoint(0, 50), 50, -Math.PI / 4, Math.PI / 4),
            new PlanLine(new PlanPoint(50, 50), new PlanPoint(50, 150))
        };

        await Assert.That(PiGeometry.FromSegments(chain, 10, out var reason)).IsNull();
        await Assert.That(reason).IsNotNull();
    }

    [Test]
    public async Task FromSegments_ArcNotTangentToItsStraights_IsRefused()
    {
        var chain = new PlanSegment[]
        {
            new PlanLine(new PlanPoint(-50, 0), new PlanPoint(0, 0)),
            new PlanArc(new PlanPoint(0, 50), 50, -Math.PI / 2, Math.PI / 2),
            new PlanLine(new PlanPoint(50, 50), new PlanPoint(100, 100)) // heads north-east, not north
        };

        await Assert.That(PiGeometry.FromSegments(chain, 10, out var reason)).IsNull();
        await Assert.That(reason.Contains("tangent")).IsTrue();
    }

    [Test]
    public async Task FromSegments_ChainThatAlreadyHasSpirals_IsRefused()
    {
        var chain = PiGeometry.Create(new[] { P(0, 0), P(500, 0, 300, 80), P(800, 300) }).Segments;

        await Assert.That(PiGeometry.FromSegments(chain, 10, out var reason)).IsNull();
        await Assert.That(reason.Contains("already")).IsTrue();
    }
}

public class PlanSegmentAdapterTests : TopoAlignTest
{
    [Test]
    public async Task FromCurves_Null_Throws()
    {
        await Assert.That(Catch(() => PlanSegmentAdapter.FromCurves(null)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task FromCurves_Line_DropsZ()
    {
        var segments = PlanSegmentAdapter.FromCurves(new Curve[] { Ln(1, 2, 4, 6, 9.5) });

        var line = (PlanLine)segments.Single();
        await Assert.That(line.StartPoint.X).IsEqualTo(1.0);
        await Assert.That(line.EndPoint.Y).IsEqualTo(6.0);
        await Assert.That(line.Length).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task FromCurves_CounterClockwiseArc_HasPositiveSweep()
    {
        var arc = (PlanArc)PlanSegmentAdapter.FromCurves(new Curve[] { QuarterArcCcw(10) }).Single();

        await Assert.That(arc.Sweep).IsEqualTo(Math.PI / 2).Within(1e-9);
        await Assert.That(arc.Radius).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(arc.StartPoint.X).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(arc.EndPoint.Y).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task FromCurves_ClockwiseArc_HasNegativeSweep()
    {
        var arc = (PlanArc)PlanSegmentAdapter.FromCurves(new Curve[] { QuarterArcCw(10) }).Single();

        await Assert.That(arc.Sweep).IsEqualTo(-Math.PI / 2).Within(1e-9);
        await Assert.That(arc.IsClockwise).IsTrue();
        await Assert.That(arc.EndPoint.Y).IsEqualTo(-10.0).Within(1e-9);
    }

    [Test]
    public async Task FromCurves_ArcLengthMatchesTheRevitArc()
    {
        var revitArc = ArcThrough(0, 0, 10, 10, 20, 0);

        var arc = PlanSegmentAdapter.FromCurves(new Curve[] { revitArc }).Single();

        await Assert.That(arc.Length).IsEqualTo(revitArc.Length).Within(1e-6);
    }

    [Test]
    public async Task FromCurves_AnythingElse_IsNotSupported()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 10, 5, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);

        await Assert.That(Catch(() => PlanSegmentAdapter.FromCurves(new Curve[] { ellipse })) is NotSupportedException).IsTrue();
    }

    [Test]
    public async Task FromCurves_KeepsTheOrderOfTheChain()
    {
        var segments = PlanSegmentAdapter.FromCurves(new Curve[] { Ln(0, 0, 10, 0), QuarterArcCcw(10) });

        await Assert.That(segments.Count).IsEqualTo(2);
        await Assert.That(segments[0] is PlanLine).IsTrue();
        await Assert.That(segments[1] is PlanArc).IsTrue();
    }
}
