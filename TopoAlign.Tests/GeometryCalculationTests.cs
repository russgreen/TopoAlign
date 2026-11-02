using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Geometry;

namespace TopoAlign.Tests;

public class GeometryCalculationTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    // ---- points along a line ------------------------------------------------------------------------------------

    [Test]
    public async Task DividePoints_ReturnsEndsPlusIntermediatePoints()
    {
        var points = GeometryCalculation.DividePoints(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10, 2);

        // A, B and the interior points at 2, 4, 6 and 8
        await Assert.That(points.Count).IsEqualTo(6);
        await Assert.That(points[0].X).IsEqualTo(0.0);
        await Assert.That(points[1].X).IsEqualTo(10.0);
        var interior = points.Skip(2).Select(p => p.X).OrderBy(x => x).ToList();
        await Assert.That(interior[0]).IsEqualTo(2.0).Within(Tol);
        await Assert.That(interior[3]).IsEqualTo(8.0).Within(Tol);
    }

    [Test]
    public async Task DividePoints_InterpolatesAllThreeCoordinates()
    {
        var points = GeometryCalculation.DividePoints(new XYZ(0, 0, 0), new XYZ(10, 20, 30), Math.Sqrt(1400), Math.Sqrt(1400) / 2);

        await Assert.That(points.Count).IsEqualTo(2 + 1);
        var middle = points[2];
        await Assert.That(middle.X).IsEqualTo(5.0).Within(Tol);
        await Assert.That(middle.Y).IsEqualTo(10.0).Within(Tol);
        await Assert.That(middle.Z).IsEqualTo(15.0).Within(Tol);
    }

    [Test]
    public async Task DividePoints_IncrementLongerThanLine_ReturnsOnlyTheEnds()
    {
        var points = GeometryCalculation.DividePoints(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10, 25);

        await Assert.That(points.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Midpoint_OfTwoPointsAndOfALine()
    {
        var midpoint = GeometryCalculation.Midpoint(new XYZ(0, 0, 0), new XYZ(4, 6, 8));
        var lineMidpoint = GeometryCalculation.Midpoint(Ln(2, 2, 6, 2));

        await Assert.That(midpoint.IsAlmostEqualTo(new XYZ(2, 3, 4))).IsTrue();
        await Assert.That(lineMidpoint.IsAlmostEqualTo(new XYZ(4, 2, 0))).IsTrue();
    }

    [Test]
    public async Task Normal_OfAHorizontalLine_IsAHorizontalUnitVectorAtRightAngles()
    {
        var normal = GeometryCalculation.Normal(Ln(0, 0, 5, 0));

        await Assert.That(normal.IsAlmostEqualTo(new XYZ(0, -1, 0))).IsTrue();
        await Assert.That(normal.GetLength()).IsEqualTo(1.0).Within(Tol);
    }

    // ---- intersections ------------------------------------------------------------------------------------------

    [Test]
    public async Task LineLineIntersection_CrossingLines()
    {
        var point = GeometryCalculation.LineLineIntersection(
            new XYZ(0, 0, 0), new XYZ(1, 0, 0), new XYZ(5, -5, 0), new XYZ(0, 1, 0));

        await Assert.That(point).IsNotNull();
        await Assert.That(point.X).IsEqualTo(5.0).Within(Tol);
        await Assert.That(point.Y).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task LineLineIntersection_ParallelLines_ReturnsNull()
    {
        var point = GeometryCalculation.LineLineIntersection(
            new XYZ(0, 0, 0), new XYZ(1, 0, 0), new XYZ(0, 1, 0), new XYZ(1, 0, 0));

        await Assert.That(point).IsNull();
    }

    [Test]
    public async Task LineLineIntersection_Curves_UsesTheirEndPoints()
    {
        var point = GeometryCalculation.LineLineIntersection(Ln(0, 0, 10, 10), Ln(0, 10, 10, 0));

        await Assert.That(point).IsNotNull();
        await Assert.That(point.X).IsEqualTo(5.0).Within(Tol);
        await Assert.That(point.Y).IsEqualTo(5.0).Within(Tol);
    }

    [Test]
    public async Task LinePlaneIntersection_LineCrossingThePlane()
    {
        var line = Line.CreateBound(new XYZ(1, 2, -5), new XYZ(1, 2, 5));
        var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);

        var point = GeometryCalculation.LinePlaneIntersection(line, plane, out var parameter);

        await Assert.That(point).IsNotNull();
        await Assert.That(point.X).IsEqualTo(1.0).Within(Tol);
        await Assert.That(point.Y).IsEqualTo(2.0).Within(Tol);
        await Assert.That(point.Z).IsEqualTo(0.0).Within(Tol);
        await Assert.That(parameter).IsEqualTo(5.0).Within(Tol);
    }

    [Test]
    public async Task LinePlaneIntersection_LineParallelToThePlane_ReturnsNull()
    {
        var line = Ln(0, 0, 10, 0, 3);
        var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);

        var point = GeometryCalculation.LinePlaneIntersection(line, plane, out var parameter);

        await Assert.That(point).IsNull();
        await Assert.That(double.IsNaN(parameter)).IsTrue();
    }

    // ---- axes and boxes -----------------------------------------------------------------------------------------

    [Test]
    public async Task GetArbitraryAxes_ForVerticalNormal()
    {
        GeometryCalculation.GetArbitraryAxes(XYZ.BasisZ, out var ax, out var ay);

        await Assert.That(ax.IsAlmostEqualTo(XYZ.BasisX)).IsTrue();
        await Assert.That(ay.IsAlmostEqualTo(XYZ.BasisY)).IsTrue();
    }

    [Test]
    public async Task GetArbitraryAxes_ForHorizontalNormal()
    {
        GeometryCalculation.GetArbitraryAxes(XYZ.BasisX, out var ax, out var ay);

        await Assert.That(ax.IsAlmostEqualTo(XYZ.BasisY)).IsTrue();
        await Assert.That(ay.IsAlmostEqualTo(XYZ.BasisZ)).IsTrue();
    }

    [Test]
    public async Task GetArbitraryAxes_AreOrthonormalToTheNormal()
    {
        var normal = new XYZ(1, 2, 3).Normalize();

        GeometryCalculation.GetArbitraryAxes(normal, out var ax, out var ay);

        await Assert.That(ax.GetLength()).IsEqualTo(1.0).Within(Tol);
        await Assert.That(ay.GetLength()).IsEqualTo(1.0).Within(Tol);
        await Assert.That(ax.DotProduct(normal)).IsEqualTo(0.0).Within(Tol);
        await Assert.That(ay.DotProduct(normal)).IsEqualTo(0.0).Within(Tol);
        await Assert.That(ax.DotProduct(ay)).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task GetBottomCorners_UsesTheGivenElevationOrTheBoxMinimum()
    {
        var box = new BoundingBoxXYZ { Min = new XYZ(1, 2, 3), Max = new XYZ(5, 8, 9) };

        var atMinimum = GeometryCalculation.GetBottomCorners(box);
        var atZero = GeometryCalculation.GetBottomCorners(box, 0);

        await Assert.That(atMinimum.Length).IsEqualTo(4);
        await Assert.That(atMinimum.All(p => Math.Abs(p.Z - 3) < Tol)).IsTrue();
        await Assert.That(atZero.All(p => Math.Abs(p.Z) < Tol)).IsTrue();
        await Assert.That(atMinimum[0].IsAlmostEqualTo(new XYZ(1, 2, 3))).IsTrue();
        await Assert.That(atMinimum[1].IsAlmostEqualTo(new XYZ(5, 2, 3))).IsTrue();
        await Assert.That(atMinimum[2].IsAlmostEqualTo(new XYZ(5, 8, 3))).IsTrue();
        await Assert.That(atMinimum[3].IsAlmostEqualTo(new XYZ(1, 8, 3))).IsTrue();
    }

    [Test]
    public async Task GetBoundingBox_OfACurveLoop()
    {
        var loop = RectangleLoop(4, 3);

        var box = GeometryCalculation.GetBoundingBox(loop);

        await Assert.That(box.Min.IsAlmostEqualTo(new XYZ(0, 0, 0))).IsTrue();
        await Assert.That(box.Max.IsAlmostEqualTo(new XYZ(4, 3, 0))).IsTrue();
    }

    // ---- arcs and loops -----------------------------------------------------------------------------------------

    [Test]
    public async Task CreateArc2dFromRadiusStartAndEndPoint_SmallSagitta_HasTheRequestedRadius()
    {
        var arc = GeometryCalculation.CreateArc2dFromRadiusStartAndEndPoint(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10);

        await Assert.That(arc.Radius).IsEqualTo(10.0).Within(1e-6);
        var mid = arc.Evaluate(0.5, true);
        await Assert.That(mid.X).IsEqualTo(5.0).Within(1e-6);
        await Assert.That(mid.Y).IsEqualTo(-(10 - Math.Sqrt(75))).Within(1e-6);
    }

    [Test]
    public async Task CreateArc2dFromRadiusStartAndEndPoint_Clockwise_BulgesTheOtherWay()
    {
        var counterClockwise = GeometryCalculation.CreateArc2dFromRadiusStartAndEndPoint(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10);
        var clockwise = GeometryCalculation.CreateArc2dFromRadiusStartAndEndPoint(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10, clockwise: true);

        await Assert.That(counterClockwise.Evaluate(0.5, true).Y).IsLessThan(0.0);
        await Assert.That(clockwise.Evaluate(0.5, true).Y).IsGreaterThan(0.0);
    }

    [Test]
    public async Task CreateArc2dFromRadiusStartAndEndPoint_LargeSagitta_IsTheMajorArc()
    {
        var small = GeometryCalculation.CreateArc2dFromRadiusStartAndEndPoint(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10);
        var large = GeometryCalculation.CreateArc2dFromRadiusStartAndEndPoint(new XYZ(0, 0, 0), new XYZ(10, 0, 0), 10, largeSagitta: true);

        await Assert.That(large.Radius).IsEqualTo(10.0).Within(1e-6);
        await Assert.That(large.Length).IsGreaterThan(small.Length);
    }

    [Test]
    public async Task CreateCurveLoop_ClosesThePolygon()
    {
        var loop = GeometryCalculation.CreateCurveLoop(new List<XYZ> { new(0, 0, 0), new(4, 0, 0), new(4, 3, 0), new(0, 3, 0) });

        await Assert.That(loop.Count()).IsEqualTo(4);
        await Assert.That(loop.IsOpen()).IsFalse();
        await Assert.That(loop.GetExactLength()).IsEqualTo(14.0).Within(Tol);
    }

    [Test]
    public async Task OffsetPoints_OffsetsEachCornerOfThePolygon()
    {
        var points = new List<XYZ> { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) };

        var offset = GeometryCalculation.OffsetPoints(points, 1, XYZ.BasisZ).ToList();

        await Assert.That(offset.Count).IsEqualTo(4);
        var width = offset.Max(p => p.X) - offset.Min(p => p.X);
        await Assert.That(Math.Abs(width - 10)).IsGreaterThan(0.5);
    }

    // ---- faces --------------------------------------------------------------------------------------------------

    [Test]
    public async Task IsTopFace_And_IsBottomFace_ClassifyBoxFaces()
    {
        var faces = PlanarFaces(Box(10, 10, 5)).ToList();

        await Assert.That(faces.Count(f => GeometryCalculation.IsTopFace(f))).IsEqualTo(1);
        await Assert.That(faces.Count(f => GeometryCalculation.IsBottomFace(f))).IsEqualTo(1);

        var top = faces.Single(f => GeometryCalculation.IsTopFace(f));
        await Assert.That(top.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ)).IsTrue();
        var bottom = faces.Single(f => GeometryCalculation.IsBottomFace(f));
        await Assert.That(bottom.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate())).IsTrue();
    }

    [Test]
    public async Task CalculateMatrixForGlobalToLocalCoordinateSystem_UsesTheFaceAxes()
    {
        var top = PlanarFaces(Box(10, 10, 5)).Single(f => GeometryCalculation.IsTopFace(f));

        var matrix = GeometryCalculation.CalculateMatrixForGlobalToLocalCoordinateSystem(top);

        await Assert.That(matrix.GetLength(0)).IsEqualTo(2);
        await Assert.That(matrix.GetLength(1)).IsEqualTo(2);
        await Assert.That(matrix[0, 0]).IsEqualTo(top.XVector.X).Within(Tol);
        await Assert.That(matrix[0, 1]).IsEqualTo(top.XVector.Y).Within(Tol);
        await Assert.That(matrix[1, 0]).IsEqualTo(top.YVector.X).Within(Tol);
        await Assert.That(matrix[1, 1]).IsEqualTo(top.YVector.Y).Within(Tol);
    }
}
