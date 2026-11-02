using static TopoAlign.Tests.TestKit;
using g3;
using TopoAlign.Geometry;

namespace TopoAlign.Tests;

public class TriTriIntersectTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    /// <summary>Triangle lying in the z = 0 plane covering x, y &gt;= 0 and x + y &lt;= 10.</summary>
    private static Triangle3d GroundTriangle()
    {
        return new Triangle3d(new Vector3d(0, 0, 0), new Vector3d(10, 0, 0), new Vector3d(0, 10, 0));
    }

    /// <summary>Upright triangle in the y = 2 plane that crosses z = 0 between x = 1.5 and x = 4.5.</summary>
    private static Triangle3d CuttingTriangle()
    {
        return new Triangle3d(new Vector3d(0, 2, -5), new Vector3d(6, 2, -5), new Vector3d(3, 2, 5));
    }

    [Test]
    public async Task IntersectTriangleLists_CrossingTriangles_ReturnTheIntersectionSegment()
    {
        var points = TriTriIntersect.IntersectTriangleLists(
            new List<Triangle3d> { GroundTriangle() }, new List<Triangle3d> { CuttingTriangle() });

        await Assert.That(points).IsNotNull();
        await Assert.That(points.Count).IsEqualTo(2);
        await Assert.That(points.All(p => Math.Abs(p.y - 2) < Tol && Math.Abs(p.z) < Tol)).IsTrue();
        await Assert.That(points.Min(p => p.x)).IsEqualTo(1.5).Within(Tol);
        await Assert.That(points.Max(p => p.x)).IsEqualTo(4.5).Within(Tol);
    }

    [Test]
    public async Task IntersectTriangleLists_SeparatedTriangles_ReturnNull()
    {
        var far = new Triangle3d(new Vector3d(0, 2, 50), new Vector3d(6, 2, 50), new Vector3d(3, 2, 60));

        var points = TriTriIntersect.IntersectTriangleLists(new List<Triangle3d> { GroundTriangle() }, new List<Triangle3d> { far });

        await Assert.That(points).IsNull();
    }

    [Test]
    public async Task IntersectTriangleLists_EmptyLists_ReturnNull()
    {
        await Assert.That(TriTriIntersect.IntersectTriangleLists(new List<Triangle3d>(), new List<Triangle3d>())).IsNull();
        await Assert.That(TriTriIntersect.IntersectTriangleLists(new List<Triangle3d> { GroundTriangle() }, new List<Triangle3d>())).IsNull();
    }

    [Test]
    public async Task IntersectTriangleLists_ManyTriangles_CollectsEveryIntersection()
    {
        var second = new Triangle3d(new Vector3d(0, 4, -5), new Vector3d(6, 4, -5), new Vector3d(3, 4, 5));

        var points = TriTriIntersect.IntersectTriangleLists(
            new List<Triangle3d> { GroundTriangle() }, new List<Triangle3d> { CuttingTriangle(), second });

        await Assert.That(points.Count).IsEqualTo(4);
    }

    [Test]
    public async Task TrianglesFromGeoObj_FlatFace_IsRemeshedIntoTrianglesOnTheFace()
    {
        var top = PlanarFaces(Box(20, 20, 3)).Single(f => f.FaceNormal.Z > 0.5);

        var triangles = TriTriIntersect.TrianglesFromGeoObj(top);

        await Assert.That(triangles.Count).IsGreaterThanOrEqualTo(2);
        foreach (var triangle in triangles)
        {
            await Assert.That(triangle.V0.z).IsEqualTo(3.0).Within(Tol);
            await Assert.That(triangle.V1.z).IsEqualTo(3.0).Within(Tol);
            await Assert.That(triangle.V2.z).IsEqualTo(3.0).Within(Tol);
        }
    }

    [Test]
    public async Task TrianglesFromGeoObj_CylinderSide_ReturnsTriangles()
    {
        var side = Cylinder(5, 10).Faces.Cast<Autodesk.Revit.DB.Face>().OfType<Autodesk.Revit.DB.CylindricalFace>().First();

        var triangles = TriTriIntersect.TrianglesFromGeoObj(side);

        await Assert.That(triangles.Count).IsGreaterThan(0);
    }
}
