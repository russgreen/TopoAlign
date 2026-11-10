using TopoAlign.Grading;
using TopoAlign.Grading.Kerb;
using TopoAlign.Grading.TopoEdge;

namespace TopoAlign.Tests;

/// <summary>
/// Pins the plan-distance behaviour that BoundaryPoints, KerbPath and TopoEdgeAligner each used to compute with their own
/// private copy, so they could be moved onto one shared helper without changing any grading result.
/// </summary>
public class PlanDistanceCharacterizationTests : TopoAlignTest
{
    private static List<EdgeSegment> Square()
    {
        var a = new GradePoint(0, 0, 0);
        var b = new GradePoint(100, 0, 0);
        var c = new GradePoint(100, 100, 0);
        var d = new GradePoint(0, 100, 0);
        return new List<EdgeSegment> { new(a, b), new(b, c), new(c, d), new(d, a) };
    }

    [Test]
    public async Task BoundaryPoints_PointExactlyAtToleranceFromCorner_CountsAsCorner()
    {
        // 3-4-5 triangle: the point is exactly 5 from the corner at the origin
        var split = BoundaryPoints.Split(new[] { new GradePoint(3, 4, 1) }, Square(), 5.0);

        await Assert.That(split.MoveCorner.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BoundaryPoints_PointJustBeyondToleranceFromCorner_IsNotACorner()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(3, 4, 1) }, Square(), 4.999999);

        await Assert.That(split.MoveCorner.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BoundaryPoints_SecondPointWithinTwiceTheToleranceOfAMovedCorner_IsDropped()
    {
        var split = BoundaryPoints.Split(new[] { new GradePoint(0.5, 0, 1), new GradePoint(0, 1.0, 2) }, Square(), 0.5);

        await Assert.That(split.MoveCorner.Count).IsEqualTo(1);
    }

    [Test]
    public async Task KerbPath_LengthIsThePlanLengthIgnoringHeight()
    {
        var path = KerbPath.FromSegments(
            new List<EdgeSegment> { new(new GradePoint(0, 0, 0), new GradePoint(3, 4, 100)) },
            1.5, 2.0);

        await Assert.That(path.Length).IsEqualTo(5.0).Within(1e-12);
    }

    [Test]
    public async Task KerbPath_LengthDoesNotDependOnTheDirectionTheSegmentIsGiven()
    {
        var forward = KerbPath.FromSegments(new List<EdgeSegment> { new(new GradePoint(0, 0, 0), new GradePoint(7, 24, 0)) }, 3, 12);
        var backward = KerbPath.FromSegments(new List<EdgeSegment> { new(new GradePoint(7, 24, 0), new GradePoint(0, 0, 0)) }, 3, 12);

        await Assert.That(forward.Length).IsEqualTo(25.0).Within(1e-12);
        await Assert.That(backward.Length).IsEqualTo(forward.Length);
    }

    [Test]
    public async Task PlanMath_Distance_IgnoresHeightAndIsSymmetric()
    {
        var a = new GradePoint(1, 2, 0);
        var b = new GradePoint(4, 6, 50);

        await Assert.That(PlanMath.Distance(a, b)).IsEqualTo(5.0).Within(1e-12);
        await Assert.That(PlanMath.Distance(b, a)).IsEqualTo(PlanMath.Distance(a, b));
        await Assert.That(PlanMath.Distance(a, a)).IsEqualTo(0.0);
    }
}
