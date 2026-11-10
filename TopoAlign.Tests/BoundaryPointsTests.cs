using TopoAlign.Grading;
using TopoAlign.Grading.TopoEdge;

namespace TopoAlign.Tests;

/// <summary>How new points are sorted against a toposolid's outline, so points on its edge don't fail the whole edit.</summary>
public class BoundaryPointsTests : TopoAlignTest
{
    /// <summary>A 100 x 100 square from the origin, as the unordered edges a mesh outline gives.</summary>
    private static List<EdgeSegment> Square()
    {
        var a = new GradePoint(0, 0, 0);
        var b = new GradePoint(100, 0, 0);
        var c = new GradePoint(100, 100, 0);
        var d = new GradePoint(0, 100, 0);
        return new List<EdgeSegment> { new(c, d), new(a, b), new(d, a), new(b, c) };
    }

    [Test]
    public async Task PointInside_IsAdded()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(50, 50, 3) }, Square());

        await Assert.That(split.Add.Count).IsEqualTo(1);
        await Assert.That(split.MoveCorner.Count).IsEqualTo(0);
        await Assert.That(split.OnEdge.Count).IsEqualTo(0);
        await Assert.That(split.Outside.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PointAtACorner_IsMovedNotAdded()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(100, 100, 3) }, Square());

        await Assert.That(split.MoveCorner.Count).IsEqualTo(1);
        await Assert.That(split.MoveCorner[0].Z).IsEqualTo(3.0);
        await Assert.That(split.Add.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TwoPointsAtTheSameCorner_GiveOneMove()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(0, 0, 3), new GradePoint(0.001, 0, 4) }, Square());

        await Assert.That(split.MoveCorner.Count).IsEqualTo(1);
        await Assert.That(split.MoveCorner[0].Z).IsEqualTo(3.0);
    }

    [Test]
    public async Task PointAlongAnEdge_IsTriedSeparately()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(100, 40, 3) }, Square());

        await Assert.That(split.OnEdge.Count).IsEqualTo(1);
        await Assert.That(split.Add.Count).IsEqualTo(0);
        await Assert.That(split.MoveCorner.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PointJustOutside_IsLeftOut()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(100.5, 40, 3), new GradePoint(-5, -5, 3) }, Square());

        await Assert.That(split.Outside.Count).IsEqualTo(2);
        await Assert.That(split.Add.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PointInAHole_IsLeftOut()
    {
        var outline = Square();
        var a = new GradePoint(40, 40, 0);
        var b = new GradePoint(60, 40, 0);
        var c = new GradePoint(60, 60, 0);
        var d = new GradePoint(40, 60, 0);
        outline.AddRange(new[] { new EdgeSegment(a, b), new EdgeSegment(b, c), new EdgeSegment(c, d), new EdgeSegment(d, a) });

        var split = BoundaryPoints.Split(new[] { new GradePoint(50, 50, 3), new GradePoint(20, 20, 3) }, outline);

        await Assert.That(split.Outside.Count).IsEqualTo(1);
        await Assert.That(split.Add.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProjectOntoOutline_PutsAPointExactlyOnTheEdge_KeepingItsHeight()
    {
        var found = BoundaryPoints.TryProjectOntoOutline(new GradePoint(100.004, 40, 7), Square(), out var projected);

        await Assert.That(found).IsTrue();
        await Assert.That(projected.X).IsEqualTo(100.0).Within(1e-9);
        await Assert.That(projected.Y).IsEqualTo(40.0).Within(1e-9);
        await Assert.That(projected.Z).IsEqualTo(7.0);
    }

    [Test]
    public async Task InsetFromOutline_MovesAnEdgePointInside_FromEitherSide()
    {
        foreach (var x in new[] { 100.003, 99.997 })
        {
            var found = BoundaryPoints.TryInsetFromOutline(new GradePoint(x, 40, 7), Square(), 0.05, out var inset);

            await Assert.That(found).IsTrue();
            await Assert.That(inset.X).IsEqualTo(99.95).Within(1e-9);
            await Assert.That(inset.Y).IsEqualTo(40.0).Within(1e-9);
            await Assert.That(inset.Z).IsEqualTo(7.0);
        }
    }

    [Test]
    public async Task NoOutline_AddsEverything()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(500, 500, 3) }, new List<EdgeSegment>());

        await Assert.That(split.Add.Count).IsEqualTo(1);
    }
}
