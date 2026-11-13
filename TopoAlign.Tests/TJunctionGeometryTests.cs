using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Plan;
using TopoAlign.ViewModels.AlignmentTools;

namespace TopoAlign.Tests;

public class TJunctionGeometryTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    private static PlanSegment Line(double x0, double y0, double x1, double y1) => new PlanLine(new PlanPoint(x0, y0), new PlanPoint(x1, y1));

    private static List<PlanSegment> Main() => new() { Line(-50, 0, 50, 0) };

    [Test]
    public async Task Perpendicular_SideStoppingShort_IsExtendedToTheMainCentreline()
    {
        var side = new List<PlanSegment> { Line(0, 40, 0, 10) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Junction.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.Junction.Y).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.TrimmedSide[^1].EndPoint.Y).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.TrimmedSide[0].StartPoint.Y).IsEqualTo(40.0).Within(Tol);
    }

    [Test]
    public async Task Perpendicular_SideCrossingTheMain_IsTrimmed()
    {
        var side = new List<PlanSegment> { Line(0, 40, 0, -20) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.TrimmedSide[^1].EndPoint.Y).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task Perpendicular_KerbReturnsHaveTheRadiusAndAreTangentToBothKerbLines()
    {
        var side = new List<PlanSegment> { Line(0, 40, 0, 10) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 3.0, 3.0);

        // left of a side road heading -y is +x: kerb lines x = 3 and y = 3, so the arc runs (3, 8) to (8, 3) around (8, 8)
        var left = result.KerbLeft!;
        await Assert.That(left.Radius).IsEqualTo(5.0).Within(Tol);
        await Assert.That(left.Center.X).IsEqualTo(8.0).Within(Tol);
        await Assert.That(left.Center.Y).IsEqualTo(8.0).Within(Tol);
        await Assert.That(left.StartPoint.X).IsEqualTo(3.0).Within(Tol);
        await Assert.That(left.StartPoint.Y).IsEqualTo(8.0).Within(Tol);
        await Assert.That(left.EndPoint.X).IsEqualTo(8.0).Within(Tol);
        await Assert.That(left.EndPoint.Y).IsEqualTo(3.0).Within(Tol);
        await Assert.That(Math.Abs(left.Sweep)).IsEqualTo(Math.PI / 2.0).Within(Tol);

        var right = result.KerbRight!;
        await Assert.That(right.Center.X).IsEqualTo(-8.0).Within(Tol);
        await Assert.That(right.Center.Y).IsEqualTo(8.0).Within(Tol);
        await Assert.That(right.StartPoint.X).IsEqualTo(-3.0).Within(Tol);
        await Assert.That(right.EndPoint.Y).IsEqualTo(3.0).Within(Tol);

        // the two returns turn opposite ways
        await Assert.That(Math.Sign(left.Sweep)).IsEqualTo(-Math.Sign(right.Sweep));
    }

    [Test]
    public async Task ZeroHalfWidths_FilletBetweenTheCentrelines()
    {
        var side = new List<PlanSegment> { Line(0, 40, 0, 10) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 0.0, 0.0);

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.KerbLeft!.StartPoint.X).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.KerbLeft!.StartPoint.Y).IsEqualTo(5.0).Within(Tol);
        await Assert.That(result.KerbLeft!.EndPoint.X).IsEqualTo(5.0).Within(Tol);
        await Assert.That(result.KerbLeft!.EndPoint.Y).IsEqualTo(0.0).Within(Tol);
    }

    [Test]
    public async Task Skewed_ArcIsTangentToBothKerbLines()
    {
        // side road comes in from the upper right at 60 degrees to the main road
        var angle = Math.PI / 3.0;
        var side = new List<PlanSegment> { Line(40 * Math.Cos(angle), 40 * Math.Sin(angle), 10 * Math.Cos(angle), 10 * Math.Sin(angle)) };

        var result = TJunctionGeometry.Create(Main(), side, 4.0, 2.0, 2.0);

        await Assert.That(result.IsValid).IsTrue();
        foreach (var arc in new[] { result.KerbLeft!, result.KerbRight! })
        {
            // the main kerb line is y = 2, so the centre is the radius away from it and the end point lies on it
            await Assert.That(arc.Center.Y).IsEqualTo(2.0 + 4.0).Within(Tol);
            await Assert.That(arc.EndPoint.Y).IsEqualTo(2.0).Within(Tol);

            // the side kerb line is 2 either side of the side centreline: the centre is the radius away from it
            var distanceFromCentreline = Math.Abs(arc.Center.X * Math.Sin(angle) - arc.Center.Y * Math.Cos(angle));
            await Assert.That(distanceFromCentreline).IsEqualTo(2.0 + 4.0).Within(Tol);
            await Assert.That(arc.Radius).IsEqualTo(4.0).Within(Tol);
        }
    }

    [Test]
    public async Task SideRoadWithTwoSegments_KeepsTheEarlierOnesAndTheOriginalDirection()
    {
        // the side road is drawn away from the main road, so the junction is at its start
        var side = new List<PlanSegment> { Line(0, 10, 0, 40), Line(0, 40, 30, 40) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.TrimmedSide.Count).IsEqualTo(2);
        await Assert.That(result.TrimmedSide[0].StartPoint.Y).IsEqualTo(0.0).Within(Tol);
        await Assert.That(result.TrimmedSide[0].EndPoint.Y).IsEqualTo(40.0).Within(Tol);
        await Assert.That(result.TrimmedSide[1].EndPoint.X).IsEqualTo(30.0).Within(Tol);
    }

    [Test]
    public async Task RadiusTooLargeForTheSideRoad_IsRejected()
    {
        var side = new List<PlanSegment> { Line(0, 8, 0, 4) };

        var result = TJunctionGeometry.Create(Main(), side, 20.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task RadiusTooLargeForTheMainStraight_IsRejected()
    {
        var main = new List<PlanSegment> { Line(-5, 0, 5, 0) };
        var side = new List<PlanSegment> { Line(0, 100, 0, 10) };

        var result = TJunctionGeometry.Create(main, side, 20.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task SideRoadThatMissesTheMain_IsRejected()
    {
        var side = new List<PlanSegment> { Line(200, 40, 200, 10) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task ParallelRoads_AreRejected()
    {
        var side = new List<PlanSegment> { Line(0, 10, 40, 10) };

        var result = TJunctionGeometry.Create(Main(), side, 5.0, 3.0, 3.0);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task NonPositiveRadius_IsRejected()
    {
        var side = new List<PlanSegment> { Line(0, 40, 0, 10) };

        await Assert.That(TJunctionGeometry.Create(Main(), side, 0.0, 3.0, 3.0).IsValid).IsFalse();
    }

    [Test]
    public async Task Map_MovesLinesAndArcs()
    {
        var arc = new PlanArc(new PlanPoint(1, 1), 2.0, 0.0, Math.PI / 2.0);

        var mapped = TJunctionGeometry.Map(new PlanSegment[] { Line(0, 0, 1, 0), arc }, p => new PlanPoint(p.X + 10.0, p.Y + 5.0));

        await Assert.That(mapped[0].StartPoint.X).IsEqualTo(10.0).Within(1e-6);
        var mappedArc = (PlanArc)mapped[1];
        await Assert.That(mappedArc.Center.X).IsEqualTo(11.0).Within(Tol);
        await Assert.That(mappedArc.StartPoint.X).IsEqualTo(13.0).Within(Tol);
        await Assert.That(mappedArc.EndPoint.Y).IsEqualTo(8.0).Within(Tol);
    }
}

public class TJunctionViewModelTests : TopoAlignUiTest
{
    private Document _previousDocument;

    [Before(Test)]
    public void RememberAppDocument()
    {
        _previousDocument = App.RevitDocument;
    }

    [After(Test)]
    public void RestoreAppDocument()
    {
        App.RevitDocument = _previousDocument;
    }

    private static Element NewAlignment(Document document, string name, Curve curve)
    {
        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var json = AlignmentJsonBuilder.Build(name, new[] { curve }, internalToShared, 0.0, out var planLength);
        return document.GetElement(AlignmentApi.Create(document, name, json, planLength));
    }

    private (TJunctionViewModel ViewModel, Element Main, Element Side) NewViewModel()
    {
        var document = NewDocument();
        App.RevitDocument = document;
        var main = NewAlignment(document, "Main", Ln(-50, 0, 50, 0));
        var side = NewAlignment(document, "Side", Ln(0, 40, 0, 10));
        var viewModel = new TJunctionViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
        viewModel.KerbRadius = 5m;
        viewModel.SideHalfWidth = 3m;
        viewModel.MainHalfWidth = 3m;
        return (viewModel, main, side);
    }

    [Test]
    public async Task Constructor_StartsWithNothingSelected()
    {
        using var backup = new SettingsBackup();
        App.RevitDocument = NewDocument();

        using var viewModel = new TJunctionViewModel();

        await Assert.That(viewModel.HasMain).IsFalse();
        await Assert.That(viewModel.HasSide).IsFalse();
        await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.WindowTitle.StartsWith("T-Junction ")).IsTrue();
        await Assert.That(viewModel.Request).IsNull();
    }

    [Test]
    public async Task Pick_HidesTheDialogAndRemembersWhichAlignmentToPick()
    {
        using var backup = new SettingsBackup();
        App.RevitDocument = NewDocument();
        using var viewModel = new TJunctionViewModel();
        var hidden = false;
        viewModel.HideRequested += (s, e) => hidden = true;

        viewModel.PickSideCommand.Execute(null);

        await Assert.That(hidden).IsTrue();
        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(viewModel.PendingPick).IsEqualTo(TJunctionPick.Side);
    }

    [Test]
    public async Task SelectingTheSameAlignmentTwice_IsRejected()
    {
        using var backup = new SettingsBackup();
        var (viewModel, main, _) = NewViewModel();
        using (viewModel)
        {
            await Assert.That(viewModel.SelectMain(main)).IsTrue();
            await Assert.That(viewModel.SelectSide(main)).IsFalse();
            await Assert.That(viewModel.HasSide).IsFalse();
        }
    }

    [Test]
    public async Task CreateIsAvailableOnceBothAlignmentsAreSelected_AndBuildsTheRequest()
    {
        using var backup = new SettingsBackup();
        var (viewModel, main, side) = NewViewModel();
        using (viewModel)
        {
            await Assert.That(viewModel.SelectMain(main)).IsTrue();
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();
            await Assert.That(viewModel.SelectSide(side)).IsTrue();
            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();

            var closed = false;
            viewModel.ClosingRequest += (s, e) => closed = true;
            viewModel.CreateCommand.Execute(null);

            await Assert.That(closed).IsTrue();
            var request = viewModel.Request!;
            await Assert.That(request.SideName).IsEqualTo("Side");
            await Assert.That(request.KerbReturns.Count).IsEqualTo(2);
            await Assert.That(request.KerbReturns[0].Name).IsEqualTo("Side kerb L");
            await Assert.That(request.KerbReturns[1].Name).IsEqualTo("Side kerb R");
            await Assert.That(request.SidePlanLength).IsEqualTo(40.0).Within(1e-6);
        }
    }

    [Test]
    public async Task ARadiusThatDoesNotFit_DisablesCreateAndSaysWhy()
    {
        using var backup = new SettingsBackup();
        var (viewModel, main, side) = NewViewModel();
        using (viewModel)
        {
            viewModel.SelectMain(main);
            viewModel.SelectSide(side);

            viewModel.KerbRadius = 500m;

            await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();
            await Assert.That(viewModel.ResultMessage.Length).IsGreaterThan(0);
        }
    }
}

public class TJunctionSurfaceTests : TopoAlignTest
{
    private static PlanSegment Line(double x0, double y0, double x1, double y1) => new PlanLine(new PlanPoint(x0, y0), new PlanPoint(x1, y1));

    [Test]
    public async Task Outline_IsCounterClockwiseAndTriangulatesToItsOwnArea()
    {
        var main = new List<PlanSegment> { Line(-50, 0, 50, 0) };
        var side = new List<PlanSegment> { Line(0, 40, 0, 10) };
        var result = TJunctionGeometry.Create(main, side, 5.0, 3.0, 3.0);

        var outline = TJunctionSurface.Outline(result, p => 10.0, p => 12.0)!;
        var points = outline.Select(p => p.Point).ToList();
        var triangles = TJunctionSurface.Triangulate(points)!;

        await Assert.That(TJunctionSurface.SignedArea(points)).IsGreaterThan(0.0);
        await Assert.That(triangles.Count).IsEqualTo(points.Count - 2);

        var triangleArea = 0.0;
        foreach (var t in triangles)
        {
            var a = points[t[0]];
            var b = points[t[1]];
            var c = points[t[2]];
            triangleArea += ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2.0;
        }

        await Assert.That(triangleArea).IsEqualTo(TJunctionSurface.SignedArea(points)).Within(1e-6);
    }

    [Test]
    public async Task Outline_ElevationRunsFromTheSideRoadToTheMainRoad()
    {
        var main = new List<PlanSegment> { Line(-50, 0, 50, 0) };
        var side = new List<PlanSegment> { Line(0, 40, 0, 10) };
        var result = TJunctionGeometry.Create(main, side, 5.0, 3.0, 3.0);

        var outline = TJunctionSurface.Outline(result, p => 10.0, p => 12.0)!;

        await Assert.That(outline.Min(p => p.Elevation)).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(outline.Max(p => p.Elevation)).IsEqualTo(12.0).Within(1e-9);
    }

    [Test]
    public async Task Outline_OfAnInvalidLayout_IsNull()
    {
        var main = new List<PlanSegment> { Line(-50, 0, 50, 0) };
        var side = new List<PlanSegment> { Line(200, 40, 200, 10) };
        var result = TJunctionGeometry.Create(main, side, 5.0, 3.0, 3.0);

        await Assert.That(TJunctionSurface.Outline(result, p => 0.0, p => 0.0)).IsNull();
    }

    [Test]
    public async Task StationAlong_MeasuresToTheNearestStraight()
    {
        var chain = new List<PlanSegment> { Line(0, 0, 10, 0), Line(10, 0, 10, 20) };

        await Assert.That(TJunctionGeometry.StationAlong(chain, new PlanPoint(4, 3))).IsEqualTo(4.0).Within(1e-9);
        await Assert.That(TJunctionGeometry.StationAlong(chain, new PlanPoint(13, 5))).IsEqualTo(15.0).Within(1e-9);
    }

    [Test]
    public async Task MainLevel_UsesTheRoadElementWhereItIsHit_ElseTheAlignmentLevel_PlusTheOffset()
    {
        double Alignment(PlanPoint p) => 10.0;
        double? Road(PlanPoint p) => p.X > 0 ? 10.05 : null;

        var level = TJunctionLevels.MainLevel(Alignment, Road, 0.01);

        await Assert.That(level(new PlanPoint(1, 0))).IsEqualTo(10.06).Within(1e-6);
        await Assert.That(level(new PlanPoint(-1, 0))).IsEqualTo(10.01).Within(1e-6);
        await Assert.That(TJunctionLevels.MainLevel(Alignment, null, 0.0)(new PlanPoint(1, 0))).IsEqualTo(10.0).Within(1e-6);
    }
}

public class TJunctionFootprintTests : TopoAlignTest
{
    private const double Tol = 1e-6;

    private static PlanSegment Line(double x0, double y0, double x1, double y1) => new PlanLine(new PlanPoint(x0, y0), new PlanPoint(x1, y1));

    // main road along x, side road coming down x = 0 towards it; radius 5, half-widths 3
    private static TJunctionResult Layout() =>
        TJunctionGeometry.Create(new List<PlanSegment> { Line(-50, 0, 50, 0) }, new List<PlanSegment> { Line(0, 40, 0, 10) }, 5.0, 3.0, 3.0);

    private static bool Inside(IReadOnlyList<PlanPoint> polygon, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    [Test]
    public async Task Join_PassesThroughTheKerbReturnStartsAndFacesTheMainRoad()
    {
        var join = TJunctionFootprint.Join(Layout());

        await Assert.That(join.HasValue).IsTrue();
        await Assert.That(Math.Abs(join!.Value.Left.X)).IsEqualTo(3.0).Within(Tol);
        await Assert.That(join.Value.Left.Y).IsEqualTo(8.0).Within(Tol);
        await Assert.That(join.Value.Right.Y).IsEqualTo(8.0).Within(Tol);
        await Assert.That(join.Value.Normal.Y).IsEqualTo(-1.0).Within(Tol);
        await Assert.That(join.Value.Direction.Y).IsEqualTo(-1.0).Within(Tol);
        await Assert.That(join.Value.SignedDistance(new PlanPoint(0, 0))).IsEqualTo(8.0).Within(Tol);
        await Assert.That(join.Value.SignedDistance(new PlanPoint(0, 20))).IsEqualTo(-12.0).Within(Tol);
    }

    [Test]
    public async Task Join_ZeroHalfWidth_HasNoLine()
    {
        var result = TJunctionGeometry.Create(new List<PlanSegment> { Line(-50, 0, 50, 0) }, new List<PlanSegment> { Line(0, 40, 0, 10) }, 5.0, 0.0, 3.0);

        await Assert.That(TJunctionFootprint.Join(result).HasValue).IsFalse();
    }

    [Test]
    public async Task CutPolygon_Perpendicular_IsARectangleFromTheMainKerbToTheJoinLine()
    {
        var polygon = TJunctionFootprint.CutPolygon(Layout());

        await Assert.That(polygon).IsNotNull();
        await Assert.That(TJunctionSurface.SignedArea(polygon!)).IsEqualTo(16.0 * 5.0).Within(Tol); // x -8..8, y 3..8, counter-clockwise
        await Assert.That(Inside(polygon!, 0, 5)).IsTrue();      // between the kerbs of the side road
        await Assert.That(Inside(polygon!, 7, 3.5)).IsTrue();    // the corner inside the kerb return is cleared too
        await Assert.That(Inside(polygon!, 0, -1)).IsFalse();    // the main carriageway stays
        await Assert.That(Inside(polygon!, 0, 12)).IsFalse();    // the side road stays
        await Assert.That(Inside(polygon!, 9, 5)).IsFalse();     // beyond the end of the kerb return
    }

    [Test]
    public async Task Skewed_CutEndsAreSquareToTheMainRoadAndTheJoinIsSquareToTheSideRoad()
    {
        // the side road runs down and to the left, so it meets the main road at a skew
        var result = TJunctionGeometry.Create(new List<PlanSegment> { Line(-50, 0, 50, 0) }, new List<PlanSegment> { Line(20, 40, 0, 10) }, 5.0, 3.0, 3.0);
        await Assert.That(result.IsValid).IsTrue();

        var join = TJunctionFootprint.Join(result)!.Value;
        var polygon = TJunctionFootprint.CutPolygon(result)!;

        // the join line is square to the side road, and both kerb returns start on or beyond it
        await Assert.That((join.Right.X - join.Left.X) * join.Direction.X + (join.Right.Y - join.Left.Y) * join.Direction.Y).IsEqualTo(0.0).Within(Tol);
        await Assert.That(join.SignedDistance(result.KerbLeft!.StartPoint)).IsGreaterThanOrEqualTo(-Tol);
        await Assert.That(join.SignedDistance(result.KerbRight!.StartPoint)).IsGreaterThanOrEqualTo(-Tol);

        // the cut starts on the main kerb line (y = 3) and runs straight out, square to the main road, to the join line
        var onKerbLine = polygon.Where(p => Math.Abs(p.Y - 3.0) < Tol).ToList();
        await Assert.That(onKerbLine.Count).IsEqualTo(2);
        foreach (var start in onKerbLine)
        {
            var far = polygon.Single(p => Math.Abs(p.Y - 3.0) > Tol && Math.Abs(p.X - start.X) < Tol);
            await Assert.That(far.Y).IsGreaterThan(3.0);
            await Assert.That(join.SignedDistance(far)).IsEqualTo(0.0).Within(Tol);
        }
    }

    [Test]
    public async Task Surface_ReachesTheSquareJoinLine()
    {
        var result = TJunctionGeometry.Create(new List<PlanSegment> { Line(-50, 0, 50, 0) }, new List<PlanSegment> { Line(20, 40, 0, 10) }, 5.0, 3.0, 3.0);
        var join = TJunctionFootprint.Join(result)!.Value;

        var outline = TJunctionSurface.Outline(result, _ => 0.0, _ => 0.0)!;

        await Assert.That(outline.Any(p => PlanPoint.Distance(p.Point, join.Left) < Tol)).IsTrue();
        await Assert.That(outline.Any(p => PlanPoint.Distance(p.Point, join.Right) < Tol)).IsTrue();
        await Assert.That(TJunctionSurface.Triangulate(outline.Select(p => p.Point).ToList())).IsNotNull();
    }

    [Test]
    public async Task CutPolygon_InvalidLayout_IsNull()
    {
        var result = TJunctionGeometry.Create(new List<PlanSegment> { Line(-50, 0, 50, 0) }, new List<PlanSegment> { Line(0, 40, 0, 10) }, 0.0, 3.0, 3.0);

        await Assert.That(TJunctionFootprint.CutPolygon(result)).IsNull();
        await Assert.That(TJunctionFootprint.Join(result).HasValue).IsFalse();
    }
}
