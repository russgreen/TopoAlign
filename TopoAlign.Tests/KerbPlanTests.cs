using TopoAlign.Sweep;
using TopoAlign.Sweep.Kerb;

namespace TopoAlign.Tests;

public class KerbPlannerTests : TopoAlignTest
{
    private static PathPoint P(double x, double y, double z = 0) => new PathPoint(x, y, z);

    [Test]
    public async Task Straight_GivesTwoSquareSections()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(10, 0) }, false);

        await Assert.That(plan.Error).IsNull();
        await Assert.That(plan.Runs.Count).IsEqualTo(1);
        var run = plan.Runs[0];
        await Assert.That(run.Count).IsEqualTo(2);
        await Assert.That(run[0].DirX).IsEqualTo(1.0).Within(1e-12);
        await Assert.That(run[1].DirX).IsEqualTo(1.0).Within(1e-12);
        await Assert.That(run[0].UScale).IsEqualTo(1.0);
    }

    [Test]
    public async Task RightAngleCorner_IsMitredAndWidened()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(10, 0), P(10, 10) }, false);

        await Assert.That(plan.Runs.Count).IsEqualTo(1);
        var corner = plan.Runs[0][1];
        await Assert.That(corner.UScale).IsEqualTo(Math.Sqrt(2.0)).Within(1e-9);
        await Assert.That(corner.DirX).IsEqualTo(Math.Sqrt(0.5)).Within(1e-9);
        await Assert.That(corner.DirY).IsEqualTo(Math.Sqrt(0.5)).Within(1e-9);
    }

    [Test]
    public async Task SharpCorner_SplitsTheRunWithSquareEnds()
    {
        // a turn of about 160 degrees would stretch the section far too much
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(10, 0), P(0, 1) }, false);

        await Assert.That(plan.Runs.Count).IsEqualTo(2);
        await Assert.That(plan.Runs[0][1].UScale).IsEqualTo(1.0);
        await Assert.That(plan.Runs[0][1].DirX).IsEqualTo(1.0).Within(1e-12);
        await Assert.That(plan.Runs[1][0].UScale).IsEqualTo(1.0);
        await Assert.That(plan.Runs[1][0].DirX).IsLessThan(0.0);
    }

    [Test]
    public async Task ClosedSquare_MitresEveryCornerAndClosesTheRun()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) }, true);

        await Assert.That(plan.Error).IsNull();
        await Assert.That(plan.Runs.Count).IsEqualTo(1);
        var run = plan.Runs[0];
        await Assert.That(run.Count).IsEqualTo(5);
        await Assert.That(run.All(s => Math.Abs(s.UScale - Math.Sqrt(2.0)) < 1e-9)).IsTrue();
        await Assert.That(run[0].Centre.X).IsEqualTo(run[4].Centre.X);
        await Assert.That(run[0].DirX).IsEqualTo(run[4].DirX).Within(1e-12);
    }

    [Test]
    public async Task ClosedLoopWithASharpCorner_BecomesOneOpenRun()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(10, 0), P(0, 1) }, true);

        await Assert.That(plan.Error).IsNull();
        var sections = plan.Runs.Sum(r => r.Count);
        await Assert.That(plan.Runs.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(sections).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task ClosingPointRepeated_IsIgnored()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10), P(0, 0) }, true);

        await Assert.That(plan.Runs.Count).IsEqualTo(1);
        await Assert.That(plan.Runs[0].Count).IsEqualTo(5);
    }

    [Test]
    public async Task RepeatedPoints_AreDropped()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0), P(0, 0), P(10, 0), P(10, 0) }, false);

        await Assert.That(plan.Runs[0].Count).IsEqualTo(2);
    }

    [Test]
    public async Task SlopingPath_KeepsHeightsAndUprightSections()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0, 0), P(10, 0, 2) }, false);

        await Assert.That(plan.Runs[0][0].Centre.Z).IsEqualTo(0.0);
        await Assert.That(plan.Runs[0][1].Centre.Z).IsEqualTo(2.0);
        // the section direction is horizontal whatever the grade
        await Assert.That(plan.Runs[0][1].DirX).IsEqualTo(1.0).Within(1e-12);
    }

    [Test]
    public async Task VerticalEdge_IsAnError()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0, 0), P(0, 0, 5) }, false);

        await Assert.That(plan.Error).IsNotNull();
    }

    [Test]
    public async Task SinglePoint_IsAnError()
    {
        var plan = KerbPlanner.Plan(new[] { P(0, 0) }, false);

        await Assert.That(plan.Error).IsNotNull();
        await Assert.That(plan.Runs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GentleCurve_StaysOneRunWithScaleNearOne()
    {
        var points = Enumerable.Range(0, 20).Select(i => P(10 * Math.Cos(i * 0.1), 10 * Math.Sin(i * 0.1))).ToList();
        var plan = KerbPlanner.Plan(points, false);

        await Assert.That(plan.Runs.Count).IsEqualTo(1);
        await Assert.That(plan.Runs[0].All(s => s.UScale < 1.01)).IsTrue();
    }
}
