using TopoAlign.Alignments;
using TopoAlign.Preview;

namespace TopoAlign.Tests;

public class PlanPolylineTests : TopoAlignTest
{
    private static PlanPoint Pt(double x, double y) => new PlanPoint(x, y);

    [Test]
    public async Task FromSegments_Straight_IsTwoPoints()
    {
        var points = PlanPolyline.FromSegments(new PlanSegment[] { new PlanLine(Pt(0, 0), Pt(10, 5)) }, 0.02);

        await Assert.That(points.Count).IsEqualTo(2);
        await Assert.That(points[1].X).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(points[1].Y).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task FromSegments_Chain_JoinsWithoutRepeatingPoints()
    {
        var segments = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(500, 0, 50, 0), new PiPoint(500, 500, 0, 0) }).Segments;

        var points = PlanPolyline.FromSegments(segments, 0.02);

        await Assert.That(points[0].X).IsEqualTo(0.0).Within(1e-9);
        await Assert.That(points[points.Count - 1].X).IsEqualTo(500.0).Within(1e-6);
        await Assert.That(points[points.Count - 1].Y).IsEqualTo(500.0).Within(1e-6);
        for (var i = 1; i < points.Count; i++)
        {
            await Assert.That(PlanPoint.Distance(points[i - 1], points[i]) > 1e-9).IsTrue();
        }
    }

    [Test]
    public async Task FromSegments_Arc_StaysWithinDeviationOfTheCircle()
    {
        var arc = new PlanArc(Pt(0, 0), 100.0, 0.0, Math.PI / 2.0);

        var points = PlanPolyline.FromSegments(new PlanSegment[] { arc }, 0.02);

        await Assert.That(points.Count > 4).IsTrue();
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var mid = Pt((points[i].X + points[i + 1].X) / 2.0, (points[i].Y + points[i + 1].Y) / 2.0);
            var sag = 100.0 - Math.Sqrt(mid.X * mid.X + mid.Y * mid.Y);
            await Assert.That(sag <= 0.02 + 1e-9).IsTrue();
        }

        await Assert.That(points[points.Count - 1].X).IsEqualTo(0.0).Within(1e-6);
        await Assert.That(points[points.Count - 1].Y).IsEqualTo(100.0).Within(1e-6);
    }

    [Test]
    public async Task FromSegments_WithSpirals_ReachesTheEndOfTheLayout()
    {
        var layout = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(500, 0, 300, 80), new PiPoint(900, 400, 0, 0) });

        var points = PlanPolyline.FromSegments(layout.Segments, 0.02);

        var end = layout.Segments[layout.Segments.Count - 1].EndPoint;
        await Assert.That(PlanPoint.Distance(points[points.Count - 1], end)).IsEqualTo(0.0).Within(1e-6);
    }

    [Test]
    public async Task FromSegments_NullOrEmpty_IsEmpty()
    {
        await Assert.That(PlanPolyline.FromSegments(null!, 0.02).Count).IsEqualTo(0);
        await Assert.That(PlanPolyline.FromSegments(new List<PlanSegment>(), 0.02).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Through_ListsThePointsInOrder()
    {
        var points = PlanPolyline.Through(new[] { new PiPoint(1, 2, 0, 0), new PiPoint(3, 4, 5, 0) });

        await Assert.That(points.Count).IsEqualTo(2);
        await Assert.That(points[1].X).IsEqualTo(3.0).Within(1e-9);
        await Assert.That(points[1].Y).IsEqualTo(4.0).Within(1e-9);
    }

    [Test]
    public async Task Crosses_HaveTwoArmsPerPointCentredOnIt()
    {
        var arms = PlanPolyline.Crosses(new[] { new PiPoint(10, 20, 0, 0) }, 4.0);

        await Assert.That(arms.Count).IsEqualTo(2);
        await Assert.That(arms[0].From.X).IsEqualTo(8.0).Within(1e-9);
        await Assert.That(arms[0].To.X).IsEqualTo(12.0).Within(1e-9);
        await Assert.That(arms[1].From.Y).IsEqualTo(18.0).Within(1e-9);
        await Assert.That(arms[1].To.Y).IsEqualTo(22.0).Within(1e-9);
    }
}
