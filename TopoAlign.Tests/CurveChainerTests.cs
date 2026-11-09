using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Alignments;

namespace TopoAlign.Tests;

public class CurveChainerTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    private static void AssertPoint(XYZ actual, double x, double y)
    {
        if (Math.Abs(actual.X - x) > Tol || Math.Abs(actual.Y - y) > Tol)
        {
            throw new Exception($"Expected ({x}, {y}) but was ({actual.X}, {actual.Y}).");
        }
    }

    // ---- invalid input ----------------------------------------------------------------------------------------

    [Test]
    public async Task TryChain_Null_ReportsNoCurves()
    {
        var result = CurveChainer.TryChain(null);

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("No curves selected.");
        await Assert.That(result.Curves.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryChain_Empty_ReportsNoSupportedCurves()
    {
        var result = CurveChainer.TryChain(new List<Curve>());

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("No supported curves selected.");
    }

    [Test]
    public async Task TryChain_OnlyNullEntries_ReportsNoSupportedCurves()
    {
        var result = CurveChainer.TryChain(new Curve[] { null, null });

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("No supported curves selected.");
    }

    [Test]
    public async Task TryChain_UnsupportedCurve_ReturnsErrorInsteadOfThrowing()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 10, 5, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);

        var result = CurveChainer.TryChain(new Curve[] { ellipse });

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("Only line and arc curves are supported.");
    }

    [Test]
    public async Task TryChain_UnsupportedCurveMixedWithLines_ReturnsError()
    {
        var spline = HermiteSpline.Create(new List<XYZ> { new(0, 0, 0), new(5, 5, 0), new(10, 0, 0) }, false);

        var result = CurveChainer.TryChain(new Curve[] { Ln(-10, 0, 0, 0), spline });

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("Only line and arc curves are supported.");
    }

    // ---- continuous chains --------------------------------------------------------------------------------------

    [Test]
    public async Task TryChain_SingleLine_IsContinuous()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0) });

        await Assert.That(result.IsContinuous).IsTrue();
        await Assert.That(result.ErrorMessage).IsNull();
        await Assert.That(result.Curves.Count).IsEqualTo(1);
        await Assert.That(result.TotalLength).IsEqualTo(10.0).Within(Tol);
    }

    [Test]
    public async Task TryChain_OrderedLines_KeepsOrderAndSumsLength()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10), Ln(10, 10, 20, 10) });

        await Assert.That(result.IsContinuous).IsTrue();
        await Assert.That(result.Curves.Count).IsEqualTo(3);
        await Assert.That(result.TotalLength).IsEqualTo(30.0).Within(Tol);
        AssertPoint(result.Curves[0].GetEndPoint(0), 0, 0);
        AssertPoint(result.Curves[2].GetEndPoint(1), 20, 10);
    }

    [Test]
    public async Task TryChain_ShuffledLines_AreOrderedFromTheFreeEnd()
    {
        var c = Ln(10, 10, 20, 10);
        var a = Ln(0, 0, 10, 0);
        var b = Ln(10, 0, 10, 10);

        var result = CurveChainer.TryChain(new Curve[] { c, a, b });

        await Assert.That(result.IsContinuous).IsTrue();
        AssertPoint(result.Curves[0].GetEndPoint(0), 0, 0);
        AssertPoint(result.Curves[1].GetEndPoint(0), 10, 0);
        AssertPoint(result.Curves[2].GetEndPoint(0), 10, 10);
        AssertPoint(result.Curves[2].GetEndPoint(1), 20, 10);
    }

    [Test]
    public async Task TryChain_LinesDrawnInOppositeDirections_AreFlippedToFollowTheChain()
    {
        // Segment B is drawn backwards, segment C is drawn backwards too.
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0), Ln(10, 10, 10, 0), Ln(20, 10, 10, 10) });

        await Assert.That(result.IsContinuous).IsTrue();
        AssertPoint(result.Curves[1].GetEndPoint(0), 10, 0);
        AssertPoint(result.Curves[1].GetEndPoint(1), 10, 10);
        AssertPoint(result.Curves[2].GetEndPoint(0), 10, 10);
        AssertPoint(result.Curves[2].GetEndPoint(1), 20, 10);
    }

    [Test]
    public async Task TryChain_FirstSegmentDrawnBackwards_StillFindsTheChainStart()
    {
        // The first segment points towards the rest of the chain, so its start point touches another curve.
        var result = CurveChainer.TryChain(new Curve[] { Ln(10, 0, 0, 0), Ln(10, 0, 10, 10), Ln(10, 10, 20, 10) });

        await Assert.That(result.IsContinuous).IsTrue();
        await Assert.That(result.Curves.Count).IsEqualTo(3);
        AssertPoint(result.Curves[0].GetEndPoint(0), 0, 0);
        AssertPoint(result.Curves[0].GetEndPoint(1), 10, 0);
        AssertPoint(result.Curves[2].GetEndPoint(1), 20, 10);
    }

    [Test]
    public async Task TryChain_LineAndArc_ChainsThroughTheArc()
    {
        var arc = QuarterArcCcw(10);
        var tail = Ln(0, 10, -10, 10);
        var head = Ln(30, 0, 10, 0);

        var result = CurveChainer.TryChain(new Curve[] { tail, arc, head });

        await Assert.That(result.IsContinuous).IsTrue();
        await Assert.That(result.Curves.Count).IsEqualTo(3);
        await Assert.That(result.Curves[1] is Arc).IsTrue();
        await Assert.That(result.TotalLength).IsEqualTo(10.0 + 20.0 + 5.0 * Math.PI).Within(1e-4);
        AssertPoint(result.Curves[0].GetEndPoint(0), 30, 0);
        AssertPoint(result.Curves[2].GetEndPoint(1), -10, 10);
    }

    [Test]
    public async Task TryChain_ReversedArc_IsFlippedKeepingItsShape()
    {
        var arc = QuarterArcCcw(10);
        var reversedArc = (Arc)Reversed(arc);

        var result = CurveChainer.TryChain(new Curve[] { Ln(20, 0, 10, 0), reversedArc });

        await Assert.That(result.IsContinuous).IsTrue();
        var chainedArc = (Arc)result.Curves[1];
        AssertPoint(chainedArc.GetEndPoint(0), 10, 0);
        AssertPoint(chainedArc.GetEndPoint(1), 0, 10);
        await Assert.That(chainedArc.Radius).IsEqualTo(10.0).Within(1e-6);
    }

    [Test]
    public async Task TryChain_ClosedLoop_ChainsAllCurves()
    {
        var result = CurveChainer.TryChain(new Curve[]
        {
            Ln(0, 0, 10, 0), Ln(10, 0, 10, 10), Ln(10, 10, 0, 10), Ln(0, 10, 0, 0)
        });

        await Assert.That(result.IsContinuous).IsTrue();
        await Assert.That(result.Curves.Count).IsEqualTo(4);
        await Assert.That(result.TotalLength).IsEqualTo(40.0).Within(Tol);
    }

    [Test]
    public async Task TryChain_SkipsNullEntries()
    {
        var result = CurveChainer.TryChain(new Curve[] { null, Ln(0, 0, 5, 0), null, Ln(5, 0, 5, 5) });

        await Assert.That(result.IsContinuous).IsTrue();
        await Assert.That(result.Curves.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TryChain_CurvesAtDifferentElevations_AreProjectedToPlan()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0, 5), Ln(10, 0, 20, 0, 8) });

        await Assert.That(result.IsContinuous).IsTrue();
        foreach (var curve in result.Curves)
        {
            await Assert.That(curve.GetEndPoint(0).Z).IsEqualTo(0.0).Within(Tol);
            await Assert.That(curve.GetEndPoint(1).Z).IsEqualTo(0.0).Within(Tol);
        }
    }

    [Test]
    public async Task TryChain_EndsWithinTolerance_AreJoined()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0), Ln(10.00005, 0, 20, 0) });

        await Assert.That(result.IsContinuous).IsTrue();
    }

    // ---- failures -----------------------------------------------------------------------------------------------

    [Test]
    public async Task TryChain_Gap_ReportsNotContinuous()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0), Ln(10.001, 0, 20, 0) });

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("Selected curves are not continuous.");
        await Assert.That(result.Curves.Count).IsEqualTo(0);
        await Assert.That(result.TotalLength).IsEqualTo(0.0);
    }

    [Test]
    public async Task TryChain_DisconnectedGroups_ReportsNotContinuous()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0), Ln(100, 100, 110, 100) });

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("Selected curves are not continuous.");
    }

    [Test]
    public async Task TryChain_TJunction_ReportsBranch()
    {
        var result = CurveChainer.TryChain(new Curve[] { Ln(0, 0, 10, 0), Ln(10, 0, 20, 0), Ln(10, 0, 10, 10) });

        await Assert.That(result.IsContinuous).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo("Selected curves branch at one or more points.");
        await Assert.That(result.Curves.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ChainResult_IsContinuous_RequiresCurvesAndNoError()
    {
        var empty = new ChainResult();
        var withError = new ChainResult { ErrorMessage = "bad" };
        withError.Curves.Add(Ln(0, 0, 1, 0));
        var good = new ChainResult();
        good.Curves.Add(Ln(0, 0, 1, 0));

        await Assert.That(empty.IsContinuous).IsFalse();
        await Assert.That(withError.IsContinuous).IsFalse();
        await Assert.That(good.IsContinuous).IsTrue();
    }

    // ---- ReverseChain -------------------------------------------------------------------------------------------

    [Test]
    public async Task ReverseChain_ReversesOrderAndDirectionOfEveryCurve()
    {
        var chain = new List<Curve> { Ln(0, 0, 10, 0), Ln(10, 0, 10, 10), Ln(10, 10, 20, 10) };

        var reversed = CurveChainer.ReverseChain(chain);

        await Assert.That(reversed.Count).IsEqualTo(3);
        AssertPoint(reversed[0].GetEndPoint(0), 20, 10);
        AssertPoint(reversed[0].GetEndPoint(1), 10, 10);
        AssertPoint(reversed[2].GetEndPoint(0), 10, 0);
        AssertPoint(reversed[2].GetEndPoint(1), 0, 0);

        // the reversed list is still a continuous chain
        var rechained = CurveChainer.TryChain(reversed);
        await Assert.That(rechained.IsContinuous).IsTrue();
    }

    [Test]
    public async Task ReverseChain_Arc_KeepsMidpointAndRadius()
    {
        var arc = QuarterArcCcw(10);

        var reversed = CurveChainer.ReverseChain(new List<Curve> { arc });

        var reversedArc = (Arc)reversed[0];
        AssertPoint(reversedArc.GetEndPoint(0), 0, 10);
        AssertPoint(reversedArc.GetEndPoint(1), 10, 0);
        await Assert.That(reversedArc.Radius).IsEqualTo(10.0).Within(1e-6);
        var mid = reversedArc.Evaluate(0.5, true);
        await Assert.That(mid.X).IsEqualTo(10 * Math.Sqrt(0.5)).Within(1e-6);
        await Assert.That(mid.Y).IsEqualTo(10 * Math.Sqrt(0.5)).Within(1e-6);
    }

    [Test]
    public async Task ReverseChain_Empty_ReturnsEmpty()
    {
        var reversed = CurveChainer.ReverseChain(new List<Curve>());

        await Assert.That(reversed.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReverseChain_UnsupportedCurve_Throws()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 10, 5, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);

        var exception = Catch(() => CurveChainer.ReverseChain(new List<Curve> { ellipse }));

        await Assert.That(exception is NotSupportedException).IsTrue();
    }
}
