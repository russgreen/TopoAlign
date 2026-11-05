using TopoAlign.Grading;

namespace TopoAlign.Tests;

/// <summary>Which points make two neighbouring topographies meet along a shared edge, worked out from coordinates alone.</summary>
public class TopoEdgeAlignerTests : TopoAlignTest
{
    private static GradePoint P(double x, double y, double z = 0.0) => new(x, y, z);

    /// <summary>A rectangle's outline from the given corner points (corners in order, closed).</summary>
    private static List<EdgeSegment> Outline(params GradePoint[] ring)
    {
        var segments = new List<EdgeSegment>();
        for (var i = 0; i < ring.Length; i++)
        {
            segments.Add(new EdgeSegment(ring[i], ring[(i + 1) % ring.Length]));
        }

        return segments;
    }

    // west topo: x 0..10, with an extra edge point at (10, 5)
    private static List<EdgeSegment> West(double z = 0.0) =>
        Outline(P(0, 0, z), P(10, 0, z), P(10, 5, z + 1), P(10, 10, z), P(0, 10, z));

    // east topo: x 10..20, with its own edge point at (10, 2)
    private static List<EdgeSegment> East(double z = 0.0) =>
        Outline(P(10, 0, z), P(20, 0, z), P(20, 10, z), P(10, 10, z), P(10, 2, z + 0.5));

    [Test]
    public async Task EdgePointsOfTheNeighbour_AreAddedAlongTheSharedEdge()
    {
        var result = TopoEdgeAligner.Align(West(), East(), true);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(1);
        await Assert.That(result.AddToFirst[0].X).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(result.AddToFirst[0].Y).IsEqualTo(2.0).Within(1e-9);
        await Assert.That(result.AddToFirst[0].Z).IsEqualTo(0.5).Within(1e-9);

        await Assert.That(result.AddToSecond.Count).IsEqualTo(1);
        await Assert.That(result.AddToSecond[0].Y).IsEqualTo(5.0).Within(1e-9);
        await Assert.That(result.AddToSecond[0].Z).IsEqualTo(0.3125).Within(1e-9);
        await Assert.That(result.ChangeOnFirst.Count).IsEqualTo(1);
    }

    [Test]
    public async Task FirstOnly_LeavesTheSecondAlone()
    {
        var result = TopoEdgeAligner.Align(West(), East(), false);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(1);
        await Assert.That(result.AddToSecond.Count).IsEqualTo(0);
    }

    [Test]
    public async Task FirstOnly_AlignsEveryExistingSharedEdgePointBetweenReferenceVertices()
    {
        var first = Outline(P(0, 0), P(10, 0), P(10, 2, 8), P(10, 5, -3), P(10, 8, 20), P(10, 10), P(0, 10));
        var second = Outline(P(10, 0), P(20, 0), P(20, 10, 10), P(10, 10, 10));
        var result = TopoEdgeAligner.Align(first, second, false);

        await Assert.That(result.ChangeOnFirst.Count).IsEqualTo(4);
        foreach (var change in result.ChangeOnFirst)
        {
            await Assert.That(change.X).IsEqualTo(10.0).Within(1e-9);
            await Assert.That(change.Z).IsEqualTo(change.Y).Within(1e-9);
        }
        await Assert.That(result.AddToSecond.Count).IsEqualTo(0);
    }

    [Test]
    public async Task StoredCollinearBoundaryPoints_AreAlignedEvenWhenAbsentFromTheMesh()
    {
        var first = Outline(P(0, 0), P(10, 0), P(10, 10), P(0, 10));
        var second = Outline(P(10, 0), P(20, 0), P(20, 10, 10), P(10, 10, 10));
        var stored = new[] { P(10, 3, 9), P(10, 7, -1), P(5, 5, 30) };
        var result = TopoEdgeAligner.Align(first, second, true, firstPoints: stored);

        await Assert.That(result.ChangeOnFirst.Count).IsEqualTo(2);
        await Assert.That(result.AddToSecond.Count).IsEqualTo(2);
        foreach (var point in result.ChangeOnFirst.Concat(result.AddToSecond))
        {
            await Assert.That(point.X).IsEqualTo(10.0).Within(1e-9);
            await Assert.That(point.Z).IsEqualTo(point.Y).Within(1e-9);
        }
    }

    [Test]
    public async Task StoredBoundaryPointThatMatchesAReferenceVertex_IsMovedWithoutAddingADuplicate()
    {
        var first = Outline(P(0, 0), P(10, 0), P(10, 10), P(0, 10));
        var stored = new[] { P(0, 0), P(10, 0), P(10, 2, 9), P(10, 10), P(0, 10) };
        var result = TopoEdgeAligner.Align(first, East(), false, firstPoints: stored);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(0);
        await Assert.That(result.ChangeOnFirst.Count).IsEqualTo(1);
        await Assert.That(result.ChangeOnFirst[0].Z).IsEqualTo(0.5).Within(1e-9);
    }

    [Test]
    public async Task CornersThatMeet_AreNotAddedAgain()
    {
        // both outlines have corners at (10, 0) and (10, 10) at the same height
        var result = TopoEdgeAligner.Align(West(), East(), true);

        await Assert.That(result.AddToFirst.Any(p => Math.Abs(p.Y) < 1e-9 || Math.Abs(p.Y - 10.0) < 1e-9)).IsFalse();
        await Assert.That(result.SharedPoints).IsEqualTo(2);
    }

    [Test]
    public async Task CoincidentPointsAtDifferentHeights_MoveTheFirstToTheSecond()
    {
        var result = TopoEdgeAligner.Align(West(0.0), East(3.0), true);

        // the corners at (10, 0) and (10, 10) are 3 ft apart in height, so the first topo's corners move up to the second's
        await Assert.That(result.ChangeOnFirst.Count).IsEqualTo(3);
        foreach (var change in result.ChangeOnFirst.Where(point => Math.Abs(point.Y - 5.0) > 1e-9))
        {
            await Assert.That(change.X).IsEqualTo(10.0).Within(1e-9);
            await Assert.That(change.Z).IsEqualTo(3.0).Within(1e-9);
        }

        await Assert.That(result.ChangeOnFirst.Any(c => Math.Abs(c.Y) < 1e-9)).IsTrue();
        await Assert.That(result.ChangeOnFirst.Any(c => Math.Abs(c.Y - 10.0) < 1e-9)).IsTrue();
        await Assert.That(result.AddToFirst.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TheSecondTopo_IsNeverMoved()
    {
        var result = TopoEdgeAligner.Align(West(3.0), East(0.0), true);

        // corners differ in height, but only the first topo has changes; the second just gets points added
        await Assert.That(result.ChangeOnFirst.Where(c => Math.Abs(c.Y - 5.0) > 1e-9).All(c => Math.Abs(c.Z) < 1e-9)).IsTrue();
        await Assert.That(result.ChangeOnFirst.Single(c => Math.Abs(c.Y - 5.0) < 1e-9).Z).IsEqualTo(0.3125).Within(1e-9);
        await Assert.That(result.AddToSecond.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TopoThatOnlyTouchesAtAPoint_GetsNothingAddedAwayFromIt()
    {
        var far = Outline(P(30, 0), P(40, 0), P(40, 10), P(30, 10));

        var result = TopoEdgeAligner.Align(West(), far, true);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(0);
        await Assert.That(result.AddToSecond.Count).IsEqualTo(0);
        await Assert.That(result.SharedPoints).IsEqualTo(0);
    }

    [Test]
    public async Task EdgesThatAreAlmostTouching_WithinTolerance_CountAsShared()
    {
        // east is 0.004 ft off in plan
        var east = Outline(P(10.004, 0), P(20, 0), P(20, 10), P(10.004, 10), P(10.004, 2, 0.5));

        var result = TopoEdgeAligner.Align(West(), east, true);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(1);
    }

    [Test]
    public async Task EdgesFurtherApartThanTheTolerance_AreNotShared()
    {
        var east = Outline(P(10.5, 0), P(20, 0), P(20, 10), P(10.5, 10), P(10.5, 2, 0.5));

        var result = TopoEdgeAligner.Align(West(), east, true);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(0);
        await Assert.That(result.AddToSecond.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AlreadyMatchingEdges_NeedNothing()
    {
        var result = TopoEdgeAligner.Align(West(), West(), true);

        await Assert.That(result.AddToFirst.Count).IsEqualTo(0);
        await Assert.That(result.AddToSecond.Count).IsEqualTo(0);
        await Assert.That(result.ChangeOnFirst.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BoundarySegments_OfTwoTriangles_AreTheSquaresFourSides()
    {
        var triangles = new List<(GradePoint, GradePoint, GradePoint)>
        {
            (P(0, 0), P(1, 0), P(1, 1)),
            (P(0, 0), P(1, 1), P(0, 1)),
        };

        var boundary = TopoEdgeAligner.BoundarySegments(triangles);

        // the shared diagonal is inside, so it isn't part of the outline
        await Assert.That(boundary.Count).IsEqualTo(4);
        await Assert.That(boundary.Any(s => (s.Start.X == 0 && s.Start.Y == 0 && s.End.X == 1 && s.End.Y == 1) || (s.End.X == 0 && s.End.Y == 0 && s.Start.X == 1 && s.Start.Y == 1))).IsFalse();
    }

    [Test]
    public async Task BoundarySegments_SnapsCornersThatAreAlmostTheSame()
    {
        var triangles = new List<(GradePoint, GradePoint, GradePoint)>
        {
            (P(0, 0), P(1, 0), P(1, 1)),
            (P(0, 0), P(1.00000001, 1), P(0, 1)),
        };

        var boundary = TopoEdgeAligner.BoundarySegments(triangles);

        await Assert.That(boundary.Count).IsEqualTo(4);
    }
}
