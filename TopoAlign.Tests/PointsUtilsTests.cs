using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using g3;
using TopoAlign.Geometry;
using Line = Autodesk.Revit.DB.Line;

namespace TopoAlign.Tests;

public class PointsUtilsTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    // ---- GetPointsFromCurves ------------------------------------------------------------------------------------

    [Test]
    public async Task GetPointsFromCurves_NoCurves_ReturnsNoPoints()
    {
        var points = PointsUtils.GetPointsFromCurves(new List<Curve>());

        await Assert.That(points.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetPointsFromCurves_LineShorterThanDivide_ReturnsItsEndPoints()
    {
        var points = PointsUtils.GetPointsFromCurves(new List<Curve> { Ln(0, 0, 2, 0) }, divide: 5);

        await Assert.That(points.Count).IsEqualTo(2);
        await Assert.That(points[0].X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(points[1].X).IsEqualTo(2.0).Within(Tol);
    }

    [Test]
    public async Task GetPointsFromCurves_LongLine_IsDividedAtTheIncrement()
    {
        var points = PointsUtils.GetPointsFromCurves(new List<Curve> { Ln(0, 0, 10, 0) }, divide: 2.5);

        // both ends and the points at 2.5, 5 and 7.5
        await Assert.That(points.Count).IsEqualTo(5);
        var xs = points.Select(p => p.X).OrderBy(x => x).ToList();
        await Assert.That(xs[0]).IsEqualTo(0.0).Within(Tol);
        await Assert.That(xs[1]).IsEqualTo(2.5).Within(Tol);
        await Assert.That(xs[2]).IsEqualTo(5.0).Within(Tol);
        await Assert.That(xs[3]).IsEqualTo(7.5).Within(Tol);
        await Assert.That(xs[4]).IsEqualTo(10.0).Within(Tol);
    }

    [Test]
    public async Task GetPointsFromCurves_AppliesTheVerticalOffset()
    {
        var points = PointsUtils.GetPointsFromCurves(new List<Curve> { Ln(0, 0, 10, 0, 5) }, divide: 2.5, offset: -0.5);

        await Assert.That(points.All(p => Math.Abs(p.Z - 4.5) < Tol)).IsTrue();
    }

    [Test]
    public async Task GetPointsFromCurves_Arc_ReturnsTheTessellatedPointsWithOffset()
    {
        var points = PointsUtils.GetPointsFromCurves(new List<Curve> { QuarterArcCcw(20) }, divide: 1, offset: 1.5);

        await Assert.That(points.Count).IsGreaterThan(2);
        await Assert.That(points.All(p => Math.Abs(p.Z - 1.5) < Tol)).IsTrue();
        await Assert.That(points.All(p => Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - 20) < 0.1)).IsTrue();
    }

    [Test]
    public async Task GetPointsFromCurves_MultipleCurves_ConcatenatesTheirPoints()
    {
        var points = PointsUtils.GetPointsFromCurves(new List<Curve> { Ln(0, 0, 2, 0), Ln(2, 0, 2, 2) }, divide: 5);

        await Assert.That(points.Count).IsEqualTo(4);
    }

    // ---- GetPointsFromVector3ds -----------------------------------------------------------------------------------

    [Test]
    public async Task GetPointsFromVector3ds_ConvertsAndOffsetsZ()
    {
        var points = PointsUtils.GetPointsFromVector3ds(new List<Vector3d> { new(1, 2, 3), new(4, 5, 6) }, offset: 0.5);

        await Assert.That(points.Count).IsEqualTo(2);
        await Assert.That(points[0].IsAlmostEqualTo(new XYZ(1, 2, 3.5))).IsTrue();
        await Assert.That(points[1].IsAlmostEqualTo(new XYZ(4, 5, 6.5))).IsTrue();
    }

    [Test]
    public async Task GetPointsFromVector3ds_Empty_ReturnsEmpty()
    {
        await Assert.That(PointsUtils.GetPointsFromVector3ds(new List<Vector3d>()).Count).IsEqualTo(0);
    }

    // ---- IsLoopClosed -------------------------------------------------------------------------------------------

    [Test]
    public async Task IsLoopClosed_ClosedSquare_IsTrue()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10), Ln(10, 10, 0, 10), Ln(0, 10, 0, 0) };

        await Assert.That(PointsUtils.IsLoopClosed(curves)).IsTrue();
    }

    [Test]
    public async Task IsLoopClosed_OpenChain_IsFalse()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10), Ln(10, 10, 0, 10) };

        await Assert.That(PointsUtils.IsLoopClosed(curves)).IsFalse();
    }

    // ---- Flatten ------------------------------------------------------------------------------------------------

    [Test]
    public async Task Flatten_Point_DropsZ()
    {
        var uv = PointsUtils.Flatten(new XYZ(3, 4, 5));
        var xyz = PointsUtils.FlattenXYZ(new XYZ(3, 4, 5));

        await Assert.That(uv.U).IsEqualTo(3.0);
        await Assert.That(uv.V).IsEqualTo(4.0);
        await Assert.That(xyz.IsAlmostEqualTo(new XYZ(3, 4, 0))).IsTrue();
    }

    [Test]
    public async Task Flatten_Polygon_DropsZFromEveryVertex()
    {
        var polygon = new List<XYZ> { new(0, 0, 1), new(10, 0, 2), new(10, 10, 3) };

        var flat = PointsUtils.Flatten(polygon);

        await Assert.That(flat.Count).IsEqualTo(3);
        await Assert.That(flat[1].U).IsEqualTo(10.0);
        await Assert.That(flat[2].V).IsEqualTo(10.0);
    }

    [Test]
    public async Task Flatten_Polygons_FlattensEachPolygon()
    {
        var polygons = new List<List<XYZ>>
        {
            new() { new XYZ(0, 0, 1), new XYZ(1, 0, 1), new XYZ(1, 1, 1) },
            new() { new XYZ(5, 5, 2), new XYZ(6, 5, 2) }
        };

        var flat = PointsUtils.Flatten(polygons);

        await Assert.That(flat.Count).IsEqualTo(2);
        await Assert.That(flat[0].Count).IsEqualTo(3);
        await Assert.That(flat[1].Count).IsEqualTo(2);
        await Assert.That(flat[1][0].U).IsEqualTo(5.0);
    }
}
