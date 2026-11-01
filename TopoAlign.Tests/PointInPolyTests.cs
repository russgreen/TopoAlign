using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Drawing;
using TopoAlign.Geometry;

namespace TopoAlign.Tests;

public class PointInPolyTests : TopoAlignTest
{
    private static UVArray Square() => new(new List<UV> { new(0, 0), new(10, 0), new(10, 10), new(0, 10) });

    private static UVArray SquareClockwise() => new(new List<UV> { new(0, 0), new(0, 10), new(10, 10), new(10, 0) });

    /// <summary>An L shape: the top right quarter (x &gt; 5, y &gt; 5) is missing.</summary>
    private static UVArray LShape() => new(new List<UV> { new(0, 0), new(10, 0), new(10, 5), new(5, 5), new(5, 10), new(0, 10) });

    // ---- UVArray ------------------------------------------------------------------------------------------------

    [Test]
    public async Task UVArray_FromXyz_DropsZ()
    {
        var array = new UVArray(new List<XYZ> { new(1, 2, 3), new(4, 5, 6) });

        await Assert.That(array.Size).IsEqualTo(2);
        await Assert.That(array.Item(0).U).IsEqualTo(1.0);
        await Assert.That(array.Item(0).V).IsEqualTo(2.0);
        await Assert.That(array.Item(1).U).IsEqualTo(4.0);
    }

    [Test]
    public async Task UVArray_FromUv_CopiesTheList()
    {
        var source = new List<UV> { new(1, 2), new(3, 4) };
        var array = new UVArray(source);
        source.Clear();

        await Assert.That(array.Size).IsEqualTo(2);
        await Assert.That(array.Item(1).V).IsEqualTo(4.0);
    }

    // ---- PolygonContains ----------------------------------------------------------------------------------------

    [Test]
    [Arguments(5.0, 5.0, true)]
    [Arguments(1.0, 9.0, true)]
    [Arguments(15.0, 5.0, false)]
    [Arguments(5.0, 15.0, false)]
    [Arguments(-1.0, -1.0, false)]
    public async Task PolygonContains_Square(double u, double v, bool expected)
    {
        await Assert.That(PointInPoly.PolygonContains(Square(), new UV(u, v))).IsEqualTo(expected);
    }

    [Test]
    [Arguments(5.0, 5.0, true)]
    [Arguments(15.0, 5.0, false)]
    public async Task PolygonContains_ClockwisePolygon(double u, double v, bool expected)
    {
        await Assert.That(PointInPoly.PolygonContains(SquareClockwise(), new UV(u, v))).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0.0, 0.0)]
    [Arguments(10.0, 10.0)]
    [Arguments(5.0, 0.0)]
    [Arguments(10.0, 5.0)]
    [Arguments(0.0, 5.0)]
    public async Task PolygonContains_PointsOnTheBoundaryCountAsInside(double u, double v)
    {
        await Assert.That(PointInPoly.PolygonContains(Square(), new UV(u, v))).IsTrue();
    }

    [Test]
    public async Task PolygonContains_ConcavePolygon_ExcludesTheNotch()
    {
        await Assert.That(PointInPoly.PolygonContains(LShape(), new UV(2, 8))).IsTrue();
        await Assert.That(PointInPoly.PolygonContains(LShape(), new UV(8, 2))).IsTrue();
        await Assert.That(PointInPoly.PolygonContains(LShape(), new UV(8, 8))).IsFalse();
    }

    [Test]
    public async Task PolygonContains_Triangle()
    {
        var triangle = new UVArray(new List<UV> { new(0, 0), new(10, 0), new(5, 10) });

        await Assert.That(PointInPoly.PolygonContains(triangle, new UV(5, 3))).IsTrue();
        await Assert.That(PointInPoly.PolygonContains(triangle, new UV(1, 8))).IsFalse();
    }

    [Test]
    [Skip("Known defect: a repeated vertex makes a zero-length edge, and UV.IsOnLine treats every point as on a zero-length line.")]
    public async Task PolygonContains_IgnoresRepeatedVertices()
    {
        var polygon = new UVArray(new List<UV> { new(0, 0), new(10, 0), new(10, 0), new(10, 10), new(0, 10) });

        await Assert.That(PointInPoly.PolygonContains(polygon, new UV(5, 5))).IsTrue();
        await Assert.That(PointInPoly.PolygonContains(polygon, new UV(15, 5))).IsFalse();
    }

    [Test]
    [Skip("Known defect: UV.IsOnLine tests the infinite line through an edge, so a point collinear with an edge but outside the polygon counts as on the boundary.")]
    public async Task PolygonContains_PointInLineWithAnEdgeButOutside_IsFalse()
    {
        await Assert.That(PointInPoly.PolygonContains(Square(), new UV(20, 0))).IsFalse();
    }

    // ---- IsPointOnPolygonBoundary -------------------------------------------------------------------------------

    [Test]
    public async Task IsPointOnPolygonBoundary_VerticesAndEdges()
    {
        await Assert.That(PointInPoly.IsPointOnPolygonBoundary(Square(), new UV(0, 0))).IsTrue();
        await Assert.That(PointInPoly.IsPointOnPolygonBoundary(Square(), new UV(10, 5))).IsTrue();
        await Assert.That(PointInPoly.IsPointOnPolygonBoundary(Square(), new UV(5, 5))).IsFalse();
        await Assert.That(PointInPoly.IsPointOnPolygonBoundary(Square(), new UV(15, 5))).IsFalse();
    }

    // ---- PointInPolygon (UVArray) ---------------------------------------------------------------------------------

    [Test]
    [Arguments(5.0, 5.0, true)]
    [Arguments(15.0, 5.0, false)]
    [Arguments(5.0, -3.0, false)]
    [Arguments(0.0, 0.0, true)]
    [Arguments(10.0, 5.0, true)]
    public async Task PointInPolygon_Square(double u, double v, bool expected)
    {
        await Assert.That(PointInPoly.PointInPolygon(Square(), new UV(u, v))).IsEqualTo(expected);
    }

    [Test]
    public async Task PointInPolygon_ConcavePolygon()
    {
        await Assert.That(PointInPoly.PointInPolygon(LShape(), new UV(2, 8))).IsTrue();
        await Assert.That(PointInPoly.PointInPolygon(LShape(), new UV(8, 8))).IsFalse();
    }

    // ---- PointInPolygon (integer polygon) -------------------------------------------------------------------------

    private static int[][] IntSquare() => new[] { new[] { 0, 0 }, new[] { 10, 0 }, new[] { 10, 10 }, new[] { 0, 10 } };

    [Test]
    public async Task PointInPolygon_IntegerPolygon()
    {
        await Assert.That(PointInPoly.PointInPolygon(IntSquare(), 4, 5, 5)).IsTrue();
        await Assert.That(PointInPoly.PointInPolygon(IntSquare(), 4, 15, 5)).IsFalse();
        await Assert.That(PointInPoly.PointInPolygon(IntSquare(), 4, 5, 15)).IsFalse();
        await Assert.That(PointInPoly.PointInPolygon(IntSquare(), 4, -5, 5)).IsFalse();
    }

    [Test]
    public async Task PointInPolygon_IntegerPolygon_FewerThanThreePointsIsNeverInside()
    {
        await Assert.That(PointInPoly.PointInPolygon(new[] { new[] { 0, 0 }, new[] { 10, 10 } }, 2, 5, 5)).IsFalse();
    }

    // ---- PointInPolygon1 (angle sum) ------------------------------------------------------------------------------

    [Test]
    public async Task PointInPolygon1_AngleSum()
    {
        var square = new[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10), new PointF(0, 10) };

        await Assert.That(PointInPoly.PointInPolygon1(square, 5, 5)).IsTrue();
    }

    [Test]
    [Skip("Known defect: the angle sum is compared with 1e-7 but is accumulated in single precision, so outside points can read as inside; not used by the add-in.")]
    public async Task PointInPolygon1_OutsidePoints_AreOutside()
    {
        var square = new[] { new PointF(0, 0), new PointF(10, 0), new PointF(10, 10), new PointF(0, 10) };

        await Assert.That(PointInPoly.PointInPolygon1(square, 15, 5)).IsFalse();
        await Assert.That(PointInPoly.PointInPolygon1(square, 5, -5)).IsFalse();
    }
}
