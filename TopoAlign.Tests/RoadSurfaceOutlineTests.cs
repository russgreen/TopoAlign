using TopoAlign.Sweep;

namespace TopoAlign.Tests;

/// <summary>The outline and points of a road surface, from triangles alone (no Revit geometry involved).</summary>
public class RoadSurfaceOutlineTests : TopoAlignTest
{
    private static void Quad(RoadSurfaceOutlineBuilder builder, double x0, double x1, double y0, double y1, double z0 = 0, double z1 = 0)
    {
        builder.AddTriangle(x0, y0, z0, x1, y0, z1, x1, y1, z1);
        builder.AddTriangle(x0, y0, z0, x1, y1, z1, x0, y1, z0);
    }

    [Test]
    public async Task OneQuad_HasOneFourCornerLoop_AndFourPoints()
    {
        var builder = new RoadSurfaceOutlineBuilder();
        Quad(builder, 0, 10, 0, 5);

        var outline = builder.Build();

        await Assert.That(outline.Loops.Count).IsEqualTo(1);
        await Assert.That(outline.Loops[0].Count).IsEqualTo(4);
        await Assert.That(outline.Points.Count).IsEqualTo(4);
        await Assert.That(outline.OpenChains).IsEqualTo(0);
    }

    [Test]
    public async Task AStripOfQuads_HasOnlyItsFourCornersInTheOutline_ButAllItsPoints()
    {
        var builder = new RoadSurfaceOutlineBuilder();
        for (var i = 0; i < 3; i++)
        {
            Quad(builder, i * 10, (i + 1) * 10, 0, 10);
        }

        var outline = builder.Build();

        await Assert.That(outline.Loops.Count).IsEqualTo(1);
        await Assert.That(outline.Loops[0].Count).IsEqualTo(4);
        await Assert.That(outline.Points.Count).IsEqualTo(8);
    }

    [Test]
    public async Task AGapInTheStrip_GivesTwoLoops()
    {
        var builder = new RoadSurfaceOutlineBuilder();
        Quad(builder, 0, 10, 0, 10);
        Quad(builder, 20, 30, 0, 10);

        var outline = builder.Build();

        await Assert.That(outline.Loops.Count).IsEqualTo(2);
        await Assert.That(outline.OpenChains).IsEqualTo(0);
    }

    [Test]
    public async Task NeighbouringFacesCutDifferently_StillJoinWithoutACrack()
    {
        var builder = new RoadSurfaceOutlineBuilder();

        // left face: one long edge along x = 10 from y 0 to 10
        builder.AddTriangle(0, 0, 0, 10, 0, 0, 10, 10, 0);
        builder.AddTriangle(0, 0, 0, 10, 10, 0, 0, 10, 0);

        // right face: the same edge cut in the middle
        builder.AddTriangle(10, 0, 0, 20, 0, 0, 10, 5, 0);
        builder.AddTriangle(20, 0, 0, 20, 10, 0, 10, 5, 0);
        builder.AddTriangle(20, 10, 0, 10, 10, 0, 10, 5, 0);

        var outline = builder.Build();

        await Assert.That(outline.Loops.Count).IsEqualTo(1);
        await Assert.That(outline.Loops[0].Count).IsEqualTo(4);
        await Assert.That(outline.OpenChains).IsEqualTo(0);
    }

    [Test]
    public async Task ACrownedSurface_KeepsTheHeightOfEveryPoint()
    {
        var builder = new RoadSurfaceOutlineBuilder();

        // two faces rising to a ridge along y = 5
        builder.AddTriangle(0, 0, 0, 10, 0, 0, 10, 5, 1);
        builder.AddTriangle(0, 0, 0, 10, 5, 1, 0, 5, 1);
        builder.AddTriangle(0, 5, 1, 10, 5, 1, 10, 10, 0);
        builder.AddTriangle(0, 5, 1, 10, 10, 0, 0, 10, 0);

        var outline = builder.Build();

        await Assert.That(outline.Loops.Count).IsEqualTo(1);
        await Assert.That(outline.Loops[0].Count).IsEqualTo(4);
        await Assert.That(outline.Points.Count).IsEqualTo(6);
        await Assert.That(outline.Points.Max(point => point.Z)).IsEqualTo(1.0);
    }

    [Test]
    public async Task TwoPointsAtTheSamePlace_BecomeTheHigherOne()
    {
        var builder = new RoadSurfaceOutlineBuilder();
        Quad(builder, 0, 10, 0, 5);
        builder.AddTriangle(0, 0, 3, 10, 0, 3, 10, 5, 3);

        var outline = builder.Build();

        await Assert.That(outline.Points.Count).IsEqualTo(4);
        await Assert.That(outline.Points.Max(point => point.Z)).IsEqualTo(3.0);
    }

    [Test]
    public async Task ATriangleStandingOnEdge_IsIgnored()
    {
        var builder = new RoadSurfaceOutlineBuilder();
        builder.AddTriangle(0, 0, 0, 0, 0, 5, 10, 0, 0);

        await Assert.That(builder.TriangleCount).IsEqualTo(0);
    }
}
