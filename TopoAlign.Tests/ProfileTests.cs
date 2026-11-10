using TopoAlign.Alignments;
using TopoAlign.Alignments.Profile;
using TopoAlign.Geometry.Topography;

namespace TopoAlign.Tests;

public class ProfileSimplifierTests : TopoAlignTest
{
    private static List<ProfilePoint> Series(params (double Station, double Elevation)[] points)
    {
        return points.Select(p => new ProfilePoint(p.Station, p.Elevation)).ToList();
    }

    [Test]
    public async Task Simplify_Null_Throws()
    {
        var exception = TestKit.Catch(() => ProfileSimplifier.Simplify(null, 0.1));

        await Assert.That(exception is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Simplify_EmptyAndSinglePoint_AreReturnedAsIs()
    {
        await Assert.That(ProfileSimplifier.Simplify(new List<ProfilePoint>(), 0.1).Count).IsEqualTo(0);
        await Assert.That(ProfileSimplifier.Simplify(Series((0, 5)), 0.1).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Simplify_TwoPoints_AreReturnedAsIs()
    {
        var result = ProfileSimplifier.Simplify(Series((0, 5), (10, 7)), 0.1);

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Simplify_CollinearPoints_KeepOnlyTheEnds()
    {
        var result = ProfileSimplifier.Simplify(Series((0, 0), (10, 1), (20, 2), (30, 3), (40, 4)), 0.01);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Station).IsEqualTo(0.0);
        await Assert.That(result[1].Station).IsEqualTo(40.0);
    }

    [Test]
    public async Task Simplify_AlwaysKeepsFirstAndLastSamples()
    {
        var samples = Series((0, 3), (5, 9), (10, 1), (15, 8), (20, 2));

        var result = ProfileSimplifier.Simplify(samples, 1000.0);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Elevation).IsEqualTo(3.0);
        await Assert.That(result[result.Count - 1].Elevation).IsEqualTo(2.0);
    }

    [Test]
    public async Task Simplify_KeepsAPeakBiggerThanTheTolerance()
    {
        var result = ProfileSimplifier.Simplify(Series((0, 0), (10, 5), (20, 0)), 1.0);

        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[1].Station).IsEqualTo(10.0);
    }

    [Test]
    public async Task Simplify_DropsAPeakSmallerThanTheTolerance()
    {
        var result = ProfileSimplifier.Simplify(Series((0, 0), (10, 0.4), (20, 0)), 0.5);

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Simplify_PeakExactlyAtTheTolerance_IsDropped()
    {
        var result = ProfileSimplifier.Simplify(Series((0, 0), (10, 0.5), (20, 0)), 0.5);

        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Simplify_ZeroTolerance_KeepsEveryBend()
    {
        var samples = Series((0, 0), (10, 1), (20, 1), (30, 3), (40, 0));

        var result = ProfileSimplifier.Simplify(samples, 0.0);

        await Assert.That(result.Count).IsEqualTo(5);
    }

    [Test]
    public async Task Simplify_NegativeTolerance_ActsLikeZero()
    {
        var samples = Series((0, 0), (10, 1), (20, 1), (30, 3), (40, 0));

        await Assert.That(ProfileSimplifier.Simplify(samples, -5.0).Count).IsEqualTo(5);
    }

    [Test]
    public async Task Simplify_DoesNotChangeTheInput()
    {
        var samples = Series((0, 0), (10, 5), (20, 0));

        ProfileSimplifier.Simplify(samples, 100.0);

        await Assert.That(samples.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Simplify_ResultStaysInOrder()
    {
        var samples = Series((0, 0), (10, 4), (20, -3), (30, 6), (40, 1), (50, 9), (60, 0));

        var result = ProfileSimplifier.Simplify(samples, 0.5);

        for (var i = 1; i < result.Count; i++)
        {
            await Assert.That(result[i].Station).IsGreaterThan(result[i - 1].Station);
        }
    }

    [Test]
    public async Task Simplify_EveryDroppedSample_StaysWithinToleranceOfTheResult()
    {
        const double tolerance = 0.25;
        var samples = new List<ProfilePoint>();
        for (var i = 0; i <= 200; i++)
        {
            var station = i * 2.5;
            samples.Add(new ProfilePoint(station, 100 + 6 * Math.Sin(station / 35.0) + 0.04 * station));
        }

        var result = ProfileSimplifier.Simplify(samples, tolerance);

        await Assert.That(result.Count).IsLessThan(samples.Count);
        foreach (var sample in samples)
        {
            var segment = 1;
            while (segment < result.Count - 1 && result[segment].Station < sample.Station)
            {
                segment++;
            }

            var distance = ProfileSimplifier.VerticalDistance(result[segment - 1], result[segment], sample);
            await Assert.That(distance).IsLessThanOrEqualTo(tolerance + 1e-9);
        }
    }

    [Test]
    public async Task Simplify_TighterToleranceKeepsMorePoints()
    {
        var samples = new List<ProfilePoint>();
        for (var i = 0; i <= 100; i++)
        {
            samples.Add(new ProfilePoint(i * 5.0, 10 * Math.Sin(i / 8.0)));
        }

        var loose = ProfileSimplifier.Simplify(samples, 1.0);
        var tight = ProfileSimplifier.Simplify(samples, 0.05);

        await Assert.That(tight.Count).IsGreaterThan(loose.Count);
    }

    [Test]
    public async Task VerticalDistance_MeasuresAgainstTheChord()
    {
        var distance = ProfileSimplifier.VerticalDistance(new ProfilePoint(0, 0), new ProfilePoint(10, 10), new ProfilePoint(5, 8));

        await Assert.That(distance).IsEqualTo(3.0).Within(1e-12);
    }

    [Test]
    public async Task VerticalDistance_ZeroRunChord_UsesTheElevationDifference()
    {
        var distance = ProfileSimplifier.VerticalDistance(new ProfilePoint(4, 2), new ProfilePoint(4, 9), new ProfilePoint(4, 6));

        await Assert.That(distance).IsEqualTo(4.0).Within(1e-12);
    }

    [Test]
    public async Task MaxAbsGrade_ReportsTheSteepestSegmentInEitherDirection()
    {
        var pvis = Series((0, 0), (100, 5), (200, -10), (300, -10));

        await Assert.That(ProfileSimplifier.MaxAbsGrade(pvis)).IsEqualTo(0.15).Within(1e-12);
    }

    [Test]
    public async Task MaxAbsGrade_FewerThanTwoPoints_IsZero()
    {
        await Assert.That(ProfileSimplifier.MaxAbsGrade(new List<ProfilePoint>())).IsEqualTo(0.0);
        await Assert.That(ProfileSimplifier.MaxAbsGrade(Series((0, 5)))).IsEqualTo(0.0);
    }
}

public class ProfileLayoutTests : TopoAlignTest
{
    private static List<ProfilePoint> Pvis(params (double Station, double Elevation)[] points)
    {
        return points.Select(p => new ProfilePoint(p.Station, p.Elevation)).ToList();
    }

    private static (ProfilePoint Start, ProfilePoint End) Ends(object element)
    {
        return element switch
        {
            ProfileLine line => (line.Start, line.End),
            ProfileParabola parabola => (parabola.Start, parabola.End),
            _ => throw new InvalidOperationException("Unknown profile element.")
        };
    }

    [Test]
    public async Task Create_FewerThanTwoPoints_Throws()
    {
        await Assert.That(TestKit.Catch(() => ProfileLayout.Create(null, 0)) is ArgumentException).IsTrue();
        await Assert.That(TestKit.Catch(() => ProfileLayout.Create(Pvis((0, 0)), 0)) is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Create_StationsThatDoNotIncrease_Throw()
    {
        var repeated = TestKit.Catch(() => ProfileLayout.Create(Pvis((0, 0), (10, 1), (10, 2)), 0));
        var backwards = TestKit.Catch(() => ProfileLayout.Create(Pvis((0, 0), (10, 1), (5, 2)), 0));

        await Assert.That(repeated is ArgumentException).IsTrue();
        await Assert.That(backwards is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Create_TwoPoints_IsOneLine()
    {
        var elements = ProfileLayout.Create(Pvis((0, 5), (100, 8)), 40);

        await Assert.That(elements.Count).IsEqualTo(1);
        var line = (ProfileLine)elements[0];
        await Assert.That(line.Start.Elevation).IsEqualTo(5.0);
        await Assert.That(line.End.Station).IsEqualTo(100.0);
    }

    [Test]
    public async Task Create_WithoutCurveLength_IsAPolylineThroughEveryPvi()
    {
        var elements = ProfileLayout.Create(Pvis((0, 0), (100, 10), (200, 0), (300, 5)), 0);

        await Assert.That(elements.Count).IsEqualTo(3);
        await Assert.That(elements.All(e => e is ProfileLine)).IsTrue();
    }

    [Test]
    public async Task Create_WithCurveLength_PutsASymmetricParabolaAtAnInteriorPvi()
    {
        var elements = ProfileLayout.Create(Pvis((0, 0), (100, 10), (200, 0)), 40);

        await Assert.That(elements.Count).IsEqualTo(3);
        var parabola = (ProfileParabola)elements[1];
        await Assert.That(parabola.Pvi.Station).IsEqualTo(100.0);
        await Assert.That(parabola.Pvi.Elevation).IsEqualTo(10.0);
        await Assert.That(parabola.Start.Station).IsEqualTo(80.0).Within(1e-9);
        await Assert.That(parabola.End.Station).IsEqualTo(120.0).Within(1e-9);
        // on the tangents: 10% up into the PVI, 10% down out of it
        await Assert.That(parabola.Start.Elevation).IsEqualTo(8.0).Within(1e-9);
        await Assert.That(parabola.End.Elevation).IsEqualTo(8.0).Within(1e-9);
    }

    [Test]
    public async Task Create_CurveLength_IsLimitedToHalfTheShorterTangent()
    {
        // tangents 10 ft and 190 ft: the curve can use at most half of the 10 ft side
        var elements = ProfileLayout.Create(Pvis((0, 0), (10, 1), (200, 0)), 100);

        var parabola = elements.OfType<ProfileParabola>().Single();
        await Assert.That(parabola.Pvi.Station - parabola.Start.Station).IsEqualTo(5.0).Within(1e-9);
        await Assert.That(parabola.End.Station - parabola.Pvi.Station).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task Create_NeighbouringCurves_NeverOverlap()
    {
        var elements = ProfileLayout.Create(Pvis((0, 0), (50, 5), (100, 0), (150, 5), (200, 0)), 1000);

        var parabolas = elements.OfType<ProfileParabola>().ToList();
        await Assert.That(parabolas.Count).IsEqualTo(3);
        for (var i = 1; i < parabolas.Count; i++)
        {
            await Assert.That(parabolas[i].Start.Station).IsGreaterThanOrEqualTo(parabolas[i - 1].End.Station - 1e-9);
        }
    }

    [Test]
    public async Task Create_NoGradeChange_GetsNoParabola()
    {
        var elements = ProfileLayout.Create(Pvis((0, 0), (100, 10), (200, 20)), 40);

        await Assert.That(elements.OfType<ProfileParabola>().Count()).IsEqualTo(0);
        await Assert.That(elements.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Create_ElementsAreContinuousFromTheFirstPviToTheLast()
    {
        var pvis = Pvis((0, 0), (80, 12), (150, 3), (260, 9), (400, 2));

        var elements = ProfileLayout.Create(pvis, 60);

        await Assert.That(Ends(elements[0]).Start.Station).IsEqualTo(0.0);
        await Assert.That(Ends(elements[elements.Count - 1]).End.Station).IsEqualTo(400.0);
        for (var i = 1; i < elements.Count; i++)
        {
            var previous = Ends(elements[i - 1]).End;
            var next = Ends(elements[i]).Start;
            await Assert.That(next.Station).IsEqualTo(previous.Station).Within(1e-9);
            await Assert.That(next.Elevation).IsEqualTo(previous.Elevation).Within(1e-9);
        }
    }

    [Test]
    public async Task Create_ParabolaIsTangentToItsNeighbours()
    {
        var elements = ProfileLayout.Create(Pvis((0, 0), (100, 10), (200, 0)), 40);

        var before = (ProfileLine)elements[0];
        var parabola = (ProfileParabola)elements[1];
        var after = (ProfileLine)elements[2];

        var gradeIn = (before.End.Elevation - before.Start.Elevation) / (before.End.Station - before.Start.Station);
        var parabolaIn = (parabola.Pvi.Elevation - parabola.Start.Elevation) / (parabola.Pvi.Station - parabola.Start.Station);
        var gradeOut = (after.End.Elevation - after.Start.Elevation) / (after.End.Station - after.Start.Station);
        var parabolaOut = (parabola.End.Elevation - parabola.Pvi.Elevation) / (parabola.End.Station - parabola.Pvi.Station);

        await Assert.That(parabolaIn).IsEqualTo(gradeIn).Within(1e-9);
        await Assert.That(parabolaOut).IsEqualTo(gradeOut).Within(1e-9);
    }

    [Test]
    public async Task Create_NegativeCurveLength_ActsLikeNone()
    {
        var elements = ProfileLayout.Create(Pvis((0, 0), (100, 10), (200, 0)), -5);

        await Assert.That(elements.All(e => e is ProfileLine)).IsTrue();
    }
}

public class TriangleSurfaceTests : TopoAlignTest
{
    /// <summary>Two triangles covering the square (0,0)-(10,10) on the plane z = 2x + 3y + 1.</summary>
    private static TriangleSurface PlaneSquare()
    {
        double Z(double x, double y) => 2 * x + 3 * y + 1;
        var surface = new TriangleSurface();
        surface.AddTriangle(0, 0, Z(0, 0), 10, 0, Z(10, 0), 10, 10, Z(10, 10));
        surface.AddTriangle(0, 0, Z(0, 0), 10, 10, Z(10, 10), 0, 10, Z(0, 10));
        return surface;
    }

    [Test]
    public async Task Empty_NeverHasAnElevation()
    {
        var surface = new TriangleSurface();

        await Assert.That(surface.TryGetElevation(0, 0, out _)).IsFalse();
        await Assert.That(surface.TriangleCount).IsEqualTo(0);
    }

    [Test]
    public async Task InteriorPoint_InterpolatesThePlane()
    {
        var surface = PlaneSquare();

        var found = surface.TryGetElevation(3.5, 6.25, out var z);

        await Assert.That(found).IsTrue();
        await Assert.That(z).IsEqualTo(2 * 3.5 + 3 * 6.25 + 1).Within(1e-9);
    }

    [Test]
    public async Task PointsOnBothTrianglesOfTheSquare_AreFound()
    {
        var surface = PlaneSquare();

        await Assert.That(surface.TryGetElevation(8, 2, out _)).IsTrue();
        await Assert.That(surface.TryGetElevation(2, 8, out _)).IsTrue();
    }

    [Test]
    public async Task PointOutside_IsNotFound()
    {
        var surface = PlaneSquare();

        await Assert.That(surface.TryGetElevation(-0.5, 5, out _)).IsFalse();
        await Assert.That(surface.TryGetElevation(5, 10.5, out _)).IsFalse();
        await Assert.That(surface.TryGetElevation(50, 50, out _)).IsFalse();
    }

    [Test]
    public async Task VertexEdgeAndMaxBoundary_AreInside()
    {
        var surface = PlaneSquare();

        await Assert.That(surface.TryGetElevation(0, 0, out var corner)).IsTrue();
        await Assert.That(corner).IsEqualTo(1.0).Within(1e-9);
        await Assert.That(surface.TryGetElevation(10, 10, out var far)).IsTrue();
        await Assert.That(far).IsEqualTo(51.0).Within(1e-9);
        await Assert.That(surface.TryGetElevation(5, 5, out var diagonal)).IsTrue();
        await Assert.That(diagonal).IsEqualTo(26.0).Within(1e-9);
        await Assert.That(surface.TryGetElevation(10, 4, out var edge)).IsTrue();
        await Assert.That(edge).IsEqualTo(33.0).Within(1e-9);
    }

    [Test]
    public async Task OverlappingTriangles_ReturnTheHighest()
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(0, 0, 5, 10, 0, 5, 0, 10, 5);
        surface.AddTriangle(0, 0, 9, 10, 0, 9, 0, 10, 9);

        surface.TryGetElevation(2, 2, out var z);

        await Assert.That(z).IsEqualTo(9.0).Within(1e-9);
    }

    [Test]
    public async Task VerticalTriangle_IsIgnored()
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(0, 0, 0, 10, 0, 0, 10, 0, 10); // a wall: zero area in plan

        await Assert.That(surface.TryGetElevation(5, 0, out _)).IsFalse();
    }

    [Test]
    public async Task TrianglesAddedAfterAQuery_AreIncluded()
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(0, 0, 1, 10, 0, 1, 0, 10, 1);
        await Assert.That(surface.TryGetElevation(20, 20, out _)).IsFalse();

        surface.AddTriangle(15, 15, 3, 30, 15, 3, 15, 30, 3);

        await Assert.That(surface.TryGetElevation(20, 20, out var z)).IsTrue();
        await Assert.That(z).IsEqualTo(3.0).Within(1e-9);
    }

    [Test]
    public async Task LargeMesh_MatchesTheUnderlyingFunction()
    {
        double Z(double x, double y) => 100 + 0.04 * x + 6 * Math.Sin(x / 35.0) + 3 * Math.Cos(y / 25.0);
        var surface = new TriangleSurface();
        const double cell = 5.0;
        for (double x = 0; x < 500; x += cell)
        {
            for (double y = 0; y < 300; y += cell)
            {
                surface.AddTriangle(x, y, Z(x, y), x + cell, y, Z(x + cell, y), x + cell, y + cell, Z(x + cell, y + cell));
                surface.AddTriangle(x, y, Z(x, y), x + cell, y + cell, Z(x + cell, y + cell), x, y + cell, Z(x, y + cell));
            }
        }

        var worst = 0.0;
        for (double x = 1.3; x < 499; x += 17.7)
        {
            for (double y = 1.1; y < 299; y += 13.3)
            {
                await Assert.That(surface.TryGetElevation(x, y, out var z)).IsTrue();
                worst = Math.Max(worst, Math.Abs(z - Z(x, y)));
            }
        }

        await Assert.That(surface.TriangleCount).IsEqualTo(2 * 100 * 60);
        await Assert.That(worst).IsLessThan(0.1);
    }

    [Test]
    public async Task NegativeAndOffsetCoordinates_Work()
    {
        var surface = new TriangleSurface();
        surface.AddTriangle(-500200, 199800, 7, -500100, 199800, 7, -500200, 199900, 7);

        await Assert.That(surface.TryGetElevation(-500190, 199810, out var z)).IsTrue();
        await Assert.That(z).IsEqualTo(7.0).Within(1e-9);
        await Assert.That(surface.TryGetElevation(-500000, 199810, out _)).IsFalse();
    }
}
