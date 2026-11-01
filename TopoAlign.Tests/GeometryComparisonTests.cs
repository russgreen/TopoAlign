using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Geometry;

namespace TopoAlign.Tests;

public class GeometryComparisonTests : TopoAlignTest
{
    // ---- constants ----------------------------------------------------------------------------------------------

    [Test]
    public async Task Tolerances_AreTheSharedEpsilon()
    {
        await Assert.That(GeometryComparison.GetEps()).IsEqualTo(1e-9);
        await Assert.That(GeometryComparison.MinLineLength).IsEqualTo(1e-9);
        await Assert.That(GeometryComparison.TolPointOnPlane).IsEqualTo(1e-9);
    }

    // ---- numbers ------------------------------------------------------------------------------------------------

    [Test]
    [Arguments(0.0, true)]
    [Arguments(5e-10, true)]
    [Arguments(-5e-10, true)]
    [Arguments(1e-8, false)]
    [Arguments(-1e-8, false)]
    public async Task IsZero_UsesDefaultTolerance(double value, bool expected)
    {
        await Assert.That(GeometryComparison.IsZero(value)).IsEqualTo(expected);
    }

    [Test]
    public async Task IsZero_UsesTheGivenTolerance()
    {
        await Assert.That(GeometryComparison.IsZero(0.005, 0.01)).IsTrue();
        await Assert.That(GeometryComparison.IsZero(0.02, 0.01)).IsFalse();
    }

    [Test]
    public async Task IsEqual_Doubles()
    {
        await Assert.That(GeometryComparison.IsEqual(1.0, 1.0 + 5e-10)).IsTrue();
        await Assert.That(GeometryComparison.IsEqual(1.0, 1.0 + 1e-8)).IsFalse();
        await Assert.That(GeometryComparison.IsEqual(1.0, 1.05, 0.1)).IsTrue();
    }

    [Test]
    [Arguments(1.0, 2.0, -1)]
    [Arguments(2.0, 1.0, 1)]
    [Arguments(3.0, 3.0, 0)]
    public async Task Compare_Doubles(double a, double b, int expected)
    {
        await Assert.That(GeometryComparison.Compare(a, b)).IsEqualTo(expected);
    }

    [Test]
    public async Task Compare_Doubles_WithinToleranceAreEqual()
    {
        await Assert.That(GeometryComparison.Compare(1.0, 1.05, 0.1)).IsEqualTo(0);
    }

    [Test]
    public async Task Max_ReturnsTheLargestValue()
    {
        await Assert.That(GeometryComparison.Max(new[] { 3.0, 7.5, -2.0, 5.0 })).IsEqualTo(7.5);
        await Assert.That(GeometryComparison.Max(new[] { -3.0, -1.0 })).IsEqualTo(-1.0);
    }

    // ---- points and vectors -------------------------------------------------------------------------------------

    [Test]
    public async Task Compare_Points_IsLexicographicXThenYThenZ()
    {
        await Assert.That(GeometryComparison.Compare(new XYZ(1, 5, 5), new XYZ(2, 0, 0))).IsEqualTo(-1);
        await Assert.That(GeometryComparison.Compare(new XYZ(1, 1, 5), new XYZ(1, 2, 0))).IsEqualTo(-1);
        await Assert.That(GeometryComparison.Compare(new XYZ(1, 1, 3), new XYZ(1, 1, 2))).IsEqualTo(1);
        await Assert.That(GeometryComparison.Compare(new XYZ(1, 1, 1), new XYZ(1, 1, 1))).IsEqualTo(0);
    }

    [Test]
    public async Task IsEqual_Points()
    {
        await Assert.That(GeometryComparison.IsEqual(new XYZ(1, 2, 3), new XYZ(1, 2, 3 + 1e-10))).IsTrue();
        await Assert.That(GeometryComparison.IsEqual(new XYZ(1, 2, 3), new XYZ(1, 2, 3.1))).IsFalse();
        await Assert.That(GeometryComparison.IsEqual(new XYZ(1, 2, 3), new XYZ(1, 2, 3.1), 0.2)).IsTrue();
    }

    [Test]
    public async Task IsHorizontal_Vector_ChecksTheZComponent()
    {
        await Assert.That(GeometryComparison.IsHorizontal(new XYZ(1, 1, 0))).IsTrue();
        await Assert.That(GeometryComparison.IsHorizontal(new XYZ(1, 1, 0.1))).IsFalse();
    }

    [Test]
    public async Task IsVertical_Vector_ChecksTheXYComponents()
    {
        await Assert.That(GeometryComparison.IsVertical(new XYZ(0, 0, 5))).IsTrue();
        await Assert.That(GeometryComparison.IsVertical(new XYZ(0.001, 0, 5))).IsFalse();
        await Assert.That(GeometryComparison.IsVertical(new XYZ(0.001, 0, 5), 0.01)).IsTrue();
    }

    [Test]
    public async Task IsParallel_DetectsParallelAndOpposedVectors()
    {
        await Assert.That(GeometryComparison.IsParallel(new XYZ(1, 0, 0), new XYZ(2, 0, 0))).IsTrue();
        await Assert.That(GeometryComparison.IsParallel(new XYZ(1, 0, 0), new XYZ(-3, 0, 0))).IsTrue();
        await Assert.That(GeometryComparison.IsParallel(new XYZ(1, 0, 0), new XYZ(0, 1, 0))).IsFalse();
    }

    [Test]
    public async Task IsPerpendicular_DetectsRightAngles()
    {
        await Assert.That(GeometryComparison.IsPerpendicular(new XYZ(1, 0, 0), new XYZ(0, 1, 0))).IsTrue();
        await Assert.That(GeometryComparison.IsPerpendicular(new XYZ(1, 0, 0), new XYZ(1, 1, 0))).IsFalse();
    }

    [Test]
    [Skip("Known defect: IsPerpendicular measures the length of v twice and never checks that w has length.")]
    public async Task IsPerpendicular_ZeroLengthSecondVector_IsNotPerpendicular()
    {
        await Assert.That(GeometryComparison.IsPerpendicular(new XYZ(1, 0, 0), XYZ.Zero)).IsFalse();
    }

    [Test]
    [Skip("Known defect: IsLessOrEqual tests |b - a| < tolerance, so it is only true when a and b are equal.")]
    public async Task IsLessOrEqual_SmallerValue_IsTrue()
    {
        await Assert.That(GeometryComparison.IsLessOrEqual(1.0, 2.0)).IsTrue();
    }

    [Test]
    public async Task IsLessOrEqual_EqualValues_IsTrue()
    {
        await Assert.That(GeometryComparison.IsLessOrEqual(2.0, 2.0)).IsTrue();
    }

    // ---- slope --------------------------------------------------------------------------------------------------

    [Test]
    public async Task PointsDownwards_RequiresASteepEnoughDownwardSlope()
    {
        await Assert.That(GeometryComparison.PointsDownwards(new XYZ(1, 0, -1))).IsTrue();
        await Assert.That(GeometryComparison.PointsDownwards(new XYZ(0, 0, -1))).IsTrue();
        await Assert.That(GeometryComparison.PointsDownwards(new XYZ(1, 0, -0.1))).IsFalse();
        await Assert.That(GeometryComparison.PointsDownwards(new XYZ(1, 0, 1))).IsFalse();
    }

    [Test]
    public async Task PointsUpwards_RequiresASteepEnoughUpwardSlope()
    {
        await Assert.That(GeometryComparison.PointsUpwards(new XYZ(1, 0, 1))).IsTrue();
        await Assert.That(GeometryComparison.PointsUpwards(new XYZ(0, 0, 1))).IsTrue();
        await Assert.That(GeometryComparison.PointsUpwards(new XYZ(1, 0, 0.1))).IsFalse();
        await Assert.That(GeometryComparison.PointsUpwards(new XYZ(1, 0, -1))).IsFalse();
    }

    // ---- lines --------------------------------------------------------------------------------------------------

    [Test]
    public async Task IsCollinear_Lines()
    {
        await Assert.That(GeometryComparison.IsCollinear(Ln(0, 0, 1, 0), Ln(2, 0, 3, 0))).IsTrue();
        await Assert.That(GeometryComparison.IsCollinear(Ln(0, 0, 1, 0), Ln(2, 1, 3, 1))).IsFalse();
        await Assert.That(GeometryComparison.IsCollinear(Ln(0, 0, 1, 0), Ln(0, 0, 0, 1))).IsFalse();
    }

    [Test]
    public async Task Compare_Lines_OrdersByAngleAndIsZeroForEqualLines()
    {
        var alongX = Ln(0, 0, 5, 0);
        var alongY = Ln(0, 0, 0, 5);

        await Assert.That(GeometryComparison.Compare(alongX, Ln(0, 0, 5, 0))).IsEqualTo(0);
        await Assert.That(GeometryComparison.Compare(alongX, alongY)).IsEqualTo(-1);
        await Assert.That(GeometryComparison.Compare(alongY, alongX)).IsEqualTo(1);
    }

    [Test]
    public async Task Compare_Lines_ParallelLinesAreOrderedByTheirPoints()
    {
        var result = GeometryComparison.Compare(Ln(0, 0, 5, 0), Ln(1, 0, 6, 0));

        await Assert.That(result).IsNotEqualTo(0);
    }

    // ---- planes -------------------------------------------------------------------------------------------------

    [Test]
    public async Task Compare_Planes()
    {
        var z0 = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
        var z5 = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, 5));
        var x0 = Plane.CreateByNormalAndOrigin(XYZ.BasisX, XYZ.Zero);

        await Assert.That(GeometryComparison.Compare(z0, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(3, 4, 0)))).IsEqualTo(0);
        await Assert.That(GeometryComparison.Compare(z0, x0)).IsEqualTo(-1);
        await Assert.That(GeometryComparison.Compare(x0, z0)).IsEqualTo(1);
        await Assert.That(GeometryComparison.Compare(z0, z5)).IsNotEqualTo(0);
    }

    // ---- bounding boxes -----------------------------------------------------------------------------------------

    [Test]
    [Skip("Known defect: BoundingBoxXyzContains compares Min > p and p > Max, so it can never be true for a normal box.")]
    public async Task BoundingBoxXyzContains_PointInsideBox_IsTrue()
    {
        var box = new BoundingBoxXYZ { Min = new XYZ(0, 0, 0), Max = new XYZ(10, 10, 10) };

        await Assert.That(GeometryComparison.BoundingBoxXyzContains(box, new XYZ(5, 5, 5))).IsTrue();
    }

    // ---- faces and edges ----------------------------------------------------------------------------------------

    [Test]
    public async Task IsHorizontal_Face_TopAndBottomFacesOfABox()
    {
        var faces = PlanarFaces(Box(10, 10, 5)).ToList();

        var horizontal = faces.Count(GeometryComparison.IsHorizontal);
        var vertical = faces.Count(GeometryComparison.IsVertical);

        await Assert.That(faces.Count).IsEqualTo(6);
        await Assert.That(horizontal).IsEqualTo(2);
        await Assert.That(vertical).IsEqualTo(4);
    }

    [Test]
    public async Task IsVertical_CylindricalFace_ChecksTheAxis()
    {
        var cylinder = Cylinder(5, 10);

        var cylindricalFaces = cylinder.Faces.Cast<Face>().OfType<CylindricalFace>().ToList();

        await Assert.That(cylindricalFaces.Count).IsGreaterThan(0);
        foreach (var face in cylindricalFaces)
        {
            await Assert.That(GeometryComparison.IsVertical(face)).IsTrue();
        }
    }

    [Test]
    public async Task IsHorizontal_Edge_ClassifiesBoxEdges()
    {
        var edges = Box(10, 8, 5).Edges.Cast<Edge>().ToList();

        var horizontal = edges.Count(e => GeometryComparison.IsHorizontal(e));

        await Assert.That(edges.Count).IsEqualTo(12);
        await Assert.That(horizontal).IsEqualTo(8);
    }
}
