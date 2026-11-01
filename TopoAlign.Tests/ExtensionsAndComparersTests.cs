using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Comparers;
using TopoAlign.Extensions;
using TopoAlign.Geometry;

namespace TopoAlign.Tests;

public class ExtensionsAndComparersTests : TopoAlignTest
{
    private const double Tol = 1e-9;

    // ---- ElementId ----------------------------------------------------------------------------------------------

    [Test]
    public async Task ElementId_InvalidAndValid()
    {
        await Assert.That(ElementId.InvalidElementId.IsInvalid()).IsTrue();
        await Assert.That(ElementId.InvalidElementId.IsValid()).IsFalse();
        await Assert.That(new ElementId(12345).IsInvalid()).IsFalse();
        await Assert.That(new ElementId(12345).IsValid()).IsTrue();
    }

    // ---- Line.Contains ------------------------------------------------------------------------------------------

    [Test]
    public async Task LineContains_PointOnTheSegment()
    {
        var line = Ln(0, 0, 10, 0);

        await Assert.That(line.Contains(new XYZ(5, 0, 0))).IsTrue();
        await Assert.That(line.Contains(new XYZ(0, 0, 0))).IsTrue();
        await Assert.That(line.Contains(new XYZ(10, 0, 0))).IsTrue();
    }

    [Test]
    public async Task LineContains_PointOffTheSegment()
    {
        var line = Ln(0, 0, 10, 0);

        await Assert.That(line.Contains(new XYZ(5, 1, 0))).IsFalse();
        await Assert.That(line.Contains(new XYZ(12, 0, 0))).IsFalse();
        await Assert.That(line.Contains(new XYZ(-2, 0, 0))).IsFalse();
    }

    [Test]
    public async Task LineContains_RespectsTheTolerance()
    {
        var line = Ln(0, 0, 10, 0);

        await Assert.That(line.Contains(new XYZ(5, 0.01, 0), 1.0)).IsTrue();
    }

    // ---- Plane --------------------------------------------------------------------------------------------------

    private static Plane HorizontalPlane(double z = 0)
    {
        return Plane.Create(new Frame(new XYZ(0, 0, z), XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ));
    }

    [Test]
    public async Task Plane_SignedDistanceTo()
    {
        var plane = HorizontalPlane(2);

        await Assert.That(plane.SignedDistanceTo(new XYZ(7, 7, 5))).IsEqualTo(3.0).Within(Tol);
        await Assert.That(plane.SignedDistanceTo(new XYZ(7, 7, 0))).IsEqualTo(-2.0).Within(Tol);
        await Assert.That(plane.SignedDistanceTo(new XYZ(7, 7, 2))).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task Plane_ProjectOnto()
    {
        var projected = HorizontalPlane(2).ProjectOnto(new XYZ(1, 2, 9));

        await Assert.That(projected.IsAlmostEqualTo(new XYZ(1, 2, 2))).IsTrue();
    }

    [Test]
    public async Task Plane_ProjectInto_ReturnsCoordinatesInThePlane()
    {
        var uv = HorizontalPlane().ProjectInto(new XYZ(3, 4, 5));

        await Assert.That(uv.U).IsEqualTo(3.0).Within(Tol);
        await Assert.That(uv.V).IsEqualTo(4.0).Within(Tol);
    }

    // ---- UV -----------------------------------------------------------------------------------------------------

    [Test]
    public async Task UvIsOnLine()
    {
        await Assert.That(new UV(1, 1).IsOnLine(new UV(0, 0), new UV(2, 2))).IsTrue();
        await Assert.That(new UV(1, 2).IsOnLine(new UV(0, 0), new UV(2, 2))).IsFalse();
        await Assert.That(new UV(5, 0).IsOnLine(new UV(0, 0), new UV(10, 0))).IsTrue();
    }

    // ---- BoundingBoxXYZ -----------------------------------------------------------------------------------------

    [Test]
    public async Task BoundingBox_Clear_ProducesAnInvertedEmptyBox()
    {
        var box = new BoundingBoxXYZ();

        box.Clear();

        await Assert.That(box.Min.X).IsEqualTo(double.MaxValue);
        await Assert.That(box.Min.Z).IsEqualTo(double.MaxValue);
        await Assert.That(box.Max.X).IsEqualTo(-double.MaxValue);
        await Assert.That(box.Max.Y).IsEqualTo(-double.MaxValue);
    }

    [Test]
    public async Task BoundingBox_ExpandToContain_Point()
    {
        var box = new BoundingBoxXYZ();
        box.Clear();

        box.ExpandToContain(new XYZ(1, 2, 3));
        box.ExpandToContain(new XYZ(-4, 8, 0));

        await Assert.That(box.Min.IsAlmostEqualTo(new XYZ(-4, 2, 0))).IsTrue();
        await Assert.That(box.Max.IsAlmostEqualTo(new XYZ(1, 8, 3))).IsTrue();
    }

    [Test]
    public async Task BoundingBox_ExpandToContain_Points()
    {
        var box = new BoundingBoxXYZ();
        box.Clear();

        box.ExpandToContain(new[] { new XYZ(0, 5, 1), new XYZ(4, -2, 7), new XYZ(2, 2, 2) });

        await Assert.That(box.Min.IsAlmostEqualTo(new XYZ(0, -2, 1))).IsTrue();
        await Assert.That(box.Max.IsAlmostEqualTo(new XYZ(4, 5, 7))).IsTrue();
    }

    [Test]
    public async Task BoundingBox_ExpandToContain_OtherBox()
    {
        var box = new BoundingBoxXYZ { Min = new XYZ(0, 0, 0), Max = new XYZ(1, 1, 1) };
        var other = new BoundingBoxXYZ { Min = new XYZ(-2, 0.5, 0.5), Max = new XYZ(0.5, 4, 3) };

        box.ExpandToContain(other);

        await Assert.That(box.Min.IsAlmostEqualTo(new XYZ(-2, 0, 0))).IsTrue();
        await Assert.That(box.Max.IsAlmostEqualTo(new XYZ(1, 4, 3))).IsTrue();
    }

    // ---- JtBoundingBoxXyz ---------------------------------------------------------------------------------------

    [Test]
    public async Task JtBoundingBox_StartsInverted()
    {
        var box = new JtBoundingBoxXyz();

        await Assert.That(box.Min.X).IsEqualTo(double.MaxValue);
        await Assert.That(box.Max.X).IsEqualTo(double.MinValue);
    }

    [Test]
    public async Task JtBoundingBox_ExpandToContain_TracksMinMaxAndMidpoint()
    {
        var box = new JtBoundingBoxXyz();

        box.ExpandToContain(new XYZ(1, 2, 3));
        box.ExpandToContain(new XYZ(5, -2, 9));

        await Assert.That(box.Min.IsAlmostEqualTo(new XYZ(1, -2, 3))).IsTrue();
        await Assert.That(box.Max.IsAlmostEqualTo(new XYZ(5, 2, 9))).IsTrue();
        await Assert.That(box.MidPoint.IsAlmostEqualTo(new XYZ(3, 0, 6))).IsTrue();
    }

    [Test]
    public async Task JtBoundingBox_GetBoundingBoxOf_ManyPointLists()
    {
        var box = JtBoundingBoxXyz.GetBoundingBoxOf(new List<List<XYZ>>
        {
            new() { new XYZ(0, 0, 0), new XYZ(2, 3, 4) },
            new() { new XYZ(-1, 5, 1) }
        });

        await Assert.That(box.Min.IsAlmostEqualTo(new XYZ(-1, 0, 0))).IsTrue();
        await Assert.That(box.Max.IsAlmostEqualTo(new XYZ(2, 5, 4))).IsTrue();
    }

    // ---- Edge polygon -------------------------------------------------------------------------------------------

    [Test]
    public async Task EdgeArray_GetPolygon_ReturnsOnePointPerEdge()
    {
        var top = PlanarFaces(Box(10, 6, 2)).Single(f => f.FaceNormal.Z > 0.5);
        var edgeLoop = top.EdgeLoops.Cast<EdgeArray>().First();

        var polygon = edgeLoop.GetPolygon();

        await Assert.That(polygon.Count).IsEqualTo(4);
        await Assert.That(polygon.All(p => Math.Abs(p.Z - 2) < 1e-6)).IsTrue();
    }

    // ---- Document extensions ------------------------------------------------------------------------------------

    [Test]
    public async Task CreateDirectShape_FromAGeometryObject()
    {
        var document = NewDocument();

        DirectShape shape;
        using (var transaction = new Transaction(document, "Create direct shape"))
        {
            transaction.Start();
            shape = document.CreateDirectShape(Autodesk.Revit.DB.Point.Create(new XYZ(1, 2, 3)));
            transaction.Commit();
        }

        await Assert.That(shape).IsNotNull();
        await Assert.That(shape.Category.BuiltInCategory).IsEqualTo(BuiltInCategory.OST_GenericModel);
    }

    [Test]
    public async Task CreateDirectShape_FromSeveralObjectsAndACategory()
    {
        var document = NewDocument();

        DirectShape shape;
        using (var transaction = new Transaction(document, "Create direct shape"))
        {
            transaction.Start();
            shape = document.CreateDirectShape(
                new GeometryObject[] { Ln(0, 0, 5, 0), Ln(5, 0, 5, 5) },
                BuiltInCategory.OST_Roads);
            transaction.Commit();
        }

        await Assert.That(shape.Category.BuiltInCategory).IsEqualTo(BuiltInCategory.OST_Roads);
    }

    [Test]
    public async Task Visualize_AddsADirectShapeForThePoint()
    {
        var document = NewDocument();
        var before = new FilteredElementCollector(document).OfClass(typeof(DirectShape)).GetElementCount();

        using (var transaction = new Transaction(document, "Visualize"))
        {
            transaction.Start();
            new XYZ(1, 2, 3).Visualize(document);
            transaction.Commit();
        }

        var after = new FilteredElementCollector(document).OfClass(typeof(DirectShape)).GetElementCount();
        await Assert.That(after).IsEqualTo(before + 1);
    }

    // ---- equality comparers -------------------------------------------------------------------------------------

    [Test]
    public async Task XyzEqualityComparer_UsesASixteenthOfAnInch()
    {
        var comparer = new XyzEqualityComparer();

        await Assert.That(comparer.Equals(new XYZ(0, 0, 0), new XYZ(0.001, 0.001, 0.001))).IsTrue();
        await Assert.That(comparer.Equals(new XYZ(0, 0, 0), new XYZ(0.01, 0, 0))).IsFalse();
        await Assert.That(comparer.Equals(new XYZ(0, 0, 0), new XYZ(0, 0, 0.01))).IsFalse();
    }

    [Test]
    public async Task XyzEqualityComparer_EqualPointsHaveTheSameHash()
    {
        var comparer = new XyzEqualityComparer();

        await Assert.That(comparer.GetHashCode(new XYZ(1.234, 2, 3))).IsEqualTo(comparer.GetHashCode(new XYZ(1.234, 2, 3)));
        await Assert.That(comparer.GetHashCode(new XYZ(1, 2, 3))).IsNotEqualTo(comparer.GetHashCode(new XYZ(4, 5, 6)));
    }

    [Test]
    public async Task XyzEqualityComparer_RemovesDuplicatesWithDistinct()
    {
        var points = new List<XYZ> { new(1, 1, 1), new(1, 1, 1), new(2, 2, 2) };

        var distinct = points.Distinct(new XyzEqualityComparer()).ToList();

        await Assert.That(distinct.Count).IsEqualTo(2);
    }

    [Test]
    public async Task XyEqualityComparer_IgnoresTheElevation()
    {
        var comparer = new XyEqualityComparer();

        await Assert.That(comparer.Equals(new XYZ(1, 2, 3), new XYZ(1, 2, 300))).IsTrue();
        await Assert.That(comparer.Equals(new XYZ(1, 2, 3), new XYZ(1.1, 2, 3))).IsFalse();
        await Assert.That(comparer.Equals(new XYZ(1, 2, 3), new XYZ(1, 2.1, 3))).IsFalse();
    }

    [Test]
    public async Task XyEqualityComparer_HashIsConsistentForTheSamePoint()
    {
        var comparer = new XyEqualityComparer();

        await Assert.That(comparer.GetHashCode(new XYZ(1, 2, 3))).IsEqualTo(comparer.GetHashCode(new XYZ(1, 2, 3)));
    }
}
