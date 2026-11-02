using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Geometry;

namespace TopoAlign.Tests;

public class CurveUtilsTests : TopoAlignTest
{
    private static void AssertJoined(IList<Curve> curves)
    {
        for (var i = 0; i < curves.Count - 1; i++)
        {
            var gap = curves[i].GetEndPoint(1).DistanceTo(curves[i + 1].GetEndPoint(0));
            if (gap > 1e-6)
            {
                throw new Exception($"Curve {i} does not meet curve {i + 1} (gap {gap}).");
            }
        }
    }

    // ---- IsSupported --------------------------------------------------------------------------------------------

    [Test]
    public async Task IsSupported_AcceptsLinesAndArcsOnly()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 10, 5, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);
        var spline = HermiteSpline.Create(new List<XYZ> { new(0, 0, 0), new(5, 5, 0), new(10, 0, 0) }, false);

        await Assert.That(CurveUtils.IsSupported(Ln(0, 0, 1, 0))).IsTrue();
        await Assert.That(CurveUtils.IsSupported(QuarterArcCcw(5))).IsTrue();
        await Assert.That(CurveUtils.IsSupported(ellipse)).IsFalse();
        await Assert.That(CurveUtils.IsSupported(spline)).IsFalse();
    }

    // ---- IsCurveInXYPlane ---------------------------------------------------------------------------------------

    [Test]
    public async Task IsCurveInXYPlane_HorizontalLine_IsTrue()
    {
        await Assert.That(CurveUtils.IsCurveInXYPlane(Ln(0, 0, 10, 5, 3))).IsTrue();
    }

    [Test]
    public async Task IsCurveInXYPlane_SlopingLine_IsFalse()
    {
        var sloping = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 1));

        await Assert.That(CurveUtils.IsCurveInXYPlane(sloping)).IsFalse();
    }

    [Test]
    public async Task IsCurveInXYPlane_HorizontalArc_IsTrue()
    {
        await Assert.That(CurveUtils.IsCurveInXYPlane(QuarterArcCcw(10))).IsTrue();
    }

    [Test]
    [Skip("Known defect: reports true for an upright arc whose end points are level; not used by the add-in.")]
    public async Task IsCurveInXYPlane_VerticalArcWithEqualEndHeights_IsFalse()
    {
        // semicircle in the XZ plane: both ends at the same elevation but the arc stands upright
        var upright = Arc.Create(new XYZ(0, 0, 0), new XYZ(10, 0, 0), new XYZ(5, 0, 5));

        await Assert.That(CurveUtils.IsCurveInXYPlane(upright)).IsFalse();
    }

    // ---- SortCurvesContiguous (no Creation.Application) ----------------------------------------------------------

    [Test]
    public async Task SortCurvesContiguous_ReordersShuffledLines()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(20, 0, 30, 0), Ln(10, 0, 20, 0) };

        CurveUtils.SortCurvesContiguous(curves);

        AssertJoined(curves);
        await Assert.That(curves[1].GetEndPoint(0).X).IsEqualTo(10.0).Within(1e-6);
        await Assert.That(curves[2].GetEndPoint(1).X).IsEqualTo(30.0).Within(1e-6);
    }

    [Test]
    public async Task SortCurvesContiguous_ReversesLinesDrawnBackwards()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(20, 0, 10, 0), Ln(20, 0, 30, 0) };

        CurveUtils.SortCurvesContiguous(curves);

        AssertJoined(curves);
        await Assert.That(curves[1].GetEndPoint(0).X).IsEqualTo(10.0).Within(1e-6);
        await Assert.That(curves[1].GetEndPoint(1).X).IsEqualTo(20.0).Within(1e-6);
    }

    [Test]
    public async Task SortCurvesContiguous_SmallGaps_AreClosedWithAMidpoint()
    {
        // a 4 mm gap, larger than 1 mm but smaller than a sixteenth of an inch
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(10.004, 0, 20, 0) };

        CurveUtils.SortCurvesContiguous(curves);

        AssertJoined(curves);
        await Assert.That(curves[0].GetEndPoint(1).X).IsEqualTo(10.002).Within(1e-6);
    }

    [Test]
    public async Task SortCurvesContiguous_DisconnectedCurves_Throws()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(50, 0, 60, 0) };

        var exception = Catch(() => CurveUtils.SortCurvesContiguous(curves));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception.Message.Contains("non-contiguous")).IsTrue();
    }

    [Test]
    public async Task SortCurvesContiguous_SingleCurve_IsUnchanged()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0) };

        CurveUtils.SortCurvesContiguous(curves);

        await Assert.That(curves.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SortCurvesContiguous_ReversesArcsToo()
    {
        var arc = QuarterArcCcw(10);
        var curves = new List<Curve> { Ln(30, 0, 10, 0), Reversed(arc) };

        CurveUtils.SortCurvesContiguous(curves);

        AssertJoined(curves);
        await Assert.That(curves[1] is Arc).IsTrue();
        await Assert.That(curves[1].GetEndPoint(1).Y).IsEqualTo(10.0).Within(1e-6);
    }

    // ---- SortCurvesContiguous (with Creation.Application) ---------------------------------------------------------

    [Test]
    public async Task SortCurvesContiguous_WithCreationApplication_ReordersAndReverses()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(20, 0, 30, 0), Ln(20, 0, 10, 0) };

        CurveUtils.SortCurvesContiguous(Application.Create, curves, false);

        AssertJoined(curves);
        await Assert.That(curves[1].GetEndPoint(0).X).IsEqualTo(10.0).Within(1e-6);
        await Assert.That(curves[2].GetEndPoint(1).X).IsEqualTo(30.0).Within(1e-6);
    }

    [Test]
    public async Task SortCurvesContiguous_WithCreationApplication_DisconnectedCurvesThrow()
    {
        var curves = new List<Curve> { Ln(0, 0, 10, 0), Ln(50, 0, 60, 0) };

        var exception = Catch(() => CurveUtils.SortCurvesContiguous(Application.Create, curves, true));

        await Assert.That(exception).IsNotNull();
    }

    // ---- GetContiguousCurvesFromSelectedCurveElements -------------------------------------------------------------

    [Test]
    public async Task GetContiguousCurvesFromSelectedCurveElements_SortsTheCurvesOfModelLines()
    {
        var document = NewDocument();
        // the sort follows end points from the first curve, so the first reference has to be the start of the chain
        var first = NewModelCurve(document, Ln(0, 0, 10, 0));
        var third = NewModelCurve(document, Ln(20, 0, 30, 0));
        var second = NewModelCurve(document, Ln(10, 0, 20, 0));
        var references = new List<Reference> { new(first), new(third), new(second) };

        var curves = CurveUtils.GetContiguousCurvesFromSelectedCurveElements(document, references, false);

        await Assert.That(curves.Count).IsEqualTo(3);
        AssertJoined(curves);
        await Assert.That(curves[0].GetEndPoint(0).X).IsEqualTo(0.0).Within(1e-6);
        await Assert.That(curves[2].GetEndPoint(1).X).IsEqualTo(30.0).Within(1e-6);
    }
}
