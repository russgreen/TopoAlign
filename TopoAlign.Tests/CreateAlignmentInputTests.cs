using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;
using TopoAlign.Alignments.Plan;
using TopoAlign.Alignments.Profile;
using TopoAlign.Geometry.Topography;
using TopoAlign.PickFilters;
using TopoAlign.ViewModels.AlignmentTools;
using TopoAlign.Views.AlignmentTools;
using Settings = TopoAlign.Services.Settings;

namespace TopoAlign.Tests;

public class AlignmentJsonBuilderSegmentTests : TopoAlignTest
{
    private static PlanPoint Pt(double x, double y) => new PlanPoint(x, y);

    private static string Build(IReadOnlyList<PlanSegment> segments, Transform transform = null, double startStation = 0.0, double flatElevation = 0.0,
        IReadOnlyList<ProfilePoint> pvis = null, double minCurve = 0.0)
    {
        return AlignmentJsonBuilder.BuildFromSegments("Seg", segments, transform ?? Transform.Identity, startStation, flatElevation, pvis, minCurve, 0.005, out _);
    }

    private static List<PlanSegment> LineArcLine()
    {
        return PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(500, 0, 50, 0), new PiPoint(500, 500, 0, 0) }).Segments;
    }

    private static List<PlanSegment> WithSpirals()
    {
        return PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(500, 0, 300, 80), new PiPoint(900, 400, 0, 0) }).Segments;
    }

    [Test]
    public async Task BuildFromSegments_NullOrEmpty_Throws()
    {
        await Assert.That(Catch(() => Build(null)) is ArgumentException).IsTrue();
        await Assert.That(Catch(() => Build(new List<PlanSegment>())) is ArgumentException).IsTrue();
    }

    [Test]
    public async Task BuildFromSegments_LineArcLine_WritesTwoLinesAndACircle()
    {
        var root = Parse(Build(LineArcLine()));

        var curves = Curves(HorizontalGeometry(root));

        await Assert.That(curves.Count).IsEqualTo(3);
        await Assert.That(TypeId(curves[0]!)).IsEqualTo(LineTypeId);
        await Assert.That(TypeId(curves[1]!)).IsEqualTo(CircleTypeId);
        await Assert.That(TypeId(curves[2]!)).IsEqualTo(LineTypeId);
    }

    [Test]
    public async Task BuildFromSegments_Arc_WritesCentreStartVectorAndTurns()
    {
        var curves = Curves(HorizontalGeometry(Parse(Build(LineArcLine()))));

        var circle = curves[1]!;
        var center = PointOf(circle, Point2DKey, "center");
        var radius = PointOf(circle, Vector2DKey, "radius");
        await Assert.That(center.X).IsEqualTo(450.0).Within(1e-6);
        await Assert.That(center.Y).IsEqualTo(50.0).Within(1e-6);
        await Assert.That(Math.Sqrt(radius.X * radius.X + radius.Y * radius.Y)).IsEqualTo(50.0).Within(1e-6);
        await Assert.That(RangeOf(circle).High).IsEqualTo(0.25).Within(1e-9);
        await Assert.That(RangeOf(circle).Type).IsEqualTo(1);
        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsFalse();
    }

    [Test]
    public async Task BuildFromSegments_RightTurn_IsClockwise()
    {
        var segments = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(500, 0, 50, 0), new PiPoint(500, -500, 0, 0) }).Segments;

        var circle = Curves(HorizontalGeometry(Parse(Build(segments))))[1]!;

        await Assert.That(circle["Bool"]!["clockwise"]!.GetValue<bool>()).IsTrue();
    }

    [Test]
    public async Task BuildFromSegments_Spiral_IsWrittenAsManyShortStraights()
    {
        var curves = Curves(HorizontalGeometry(Parse(Build(WithSpirals()))));

        var lines = curves.Count(c => TypeId(c!) == LineTypeId);
        await Assert.That(curves.Count(c => TypeId(c!) == CircleTypeId)).IsEqualTo(1);
        await Assert.That(lines).IsGreaterThan(10);
    }

    [Test]
    public async Task BuildFromSegments_Spiral_PlanLengthMatchesTheTrueLengthClosely()
    {
        var segments = WithSpirals();
        AlignmentJsonBuilder.BuildFromSegments("Seg", segments, Transform.Identity, 0.0, 0.0, null, 0.0, 0.005, out var planLength);

        var trueLength = PlanSegment.TotalLength(segments);

        // straights inside a curve are shorter than the curve by a tiny amount
        await Assert.That(planLength).IsLessThanOrEqualTo(trueLength + 1e-9);
        await Assert.That(trueLength - planLength).IsLessThan(0.05);
    }

    [Test]
    public async Task BuildFromSegments_StraightsOfASpiralAreContinuous()
    {
        var curves = Curves(HorizontalGeometry(Parse(Build(WithSpirals()))));

        (double X, double Y)? previousEnd = null;
        foreach (var curve in curves)
        {
            if (TypeId(curve!) != LineTypeId)
            {
                previousEnd = null;
                continue;
            }

            var position = PointOf(curve!, Point2DKey, "position");
            if (previousEnd is not null)
            {
                await Assert.That(position.X).IsEqualTo(previousEnd.Value.X).Within(1e-6);
                await Assert.That(position.Y).IsEqualTo(previousEnd.Value.Y).Within(1e-6);
            }

            var direction = PointOf(curve!, Vector2DKey, "direction");
            previousEnd = (position.X + direction.X, position.Y + direction.Y);
        }
    }

    [Test]
    public async Task BuildFromSegments_PlanLengthIsTheSumOfTheSegments_WhenThereAreNoSpirals()
    {
        var segments = LineArcLine();
        AlignmentJsonBuilder.BuildFromSegments("Seg", segments, Transform.Identity, 0.0, 0.0, null, 0.0, 0.005, out var planLength);

        await Assert.That(planLength).IsEqualTo(PlanSegment.TotalLength(segments)).Within(1e-6);
    }

    [Test]
    public async Task BuildFromSegments_TransformMovesTheHorizontalGeometry()
    {
        var shift = Transform.CreateTranslation(new XYZ(1000, 2000, 0));

        var curves = Curves(HorizontalGeometry(Parse(Build(LineArcLine(), shift))));

        var first = PointOf(curves[0]!, Point2DKey, "position");
        await Assert.That(first.X).IsEqualTo(1000.0).Within(1e-9);
        await Assert.That(first.Y).IsEqualTo(2000.0).Within(1e-9);
        var center = PointOf(curves[1]!, Point2DKey, "center");
        await Assert.That(center.X).IsEqualTo(1450.0).Within(1e-6);
    }

    [Test]
    public async Task BuildFromSegments_FlatProfile_IsAtTheGivenInternalElevationPlusTheProjectOffset()
    {
        var shift = Transform.CreateTranslation(new XYZ(0, 0, 100));

        var profile = Curves(ProfileGeometry(Parse(Build(LineArcLine(), shift, flatElevation: 12.5))));

        await Assert.That(profile.Count).IsEqualTo(1);
        await Assert.That(PointOf(profile[0]!, Point2DKey, "position").Y).IsEqualTo(112.5).Within(1e-9);
    }

    [Test]
    public async Task BuildFromSegments_StartStation_IsWritten()
    {
        var core = CoreComponent(Parse(Build(LineArcLine(), startStation: 1234.5)));

        await Assert.That(core["Float64"]!["startstation"]!.GetValue<double>()).IsEqualTo(1234.5);
    }

    [Test]
    public async Task BuildFromSegments_Pvis_BecomeTheProfile()
    {
        var segments = LineArcLine();
        var length = PlanSegment.TotalLength(segments);
        var pvis = new List<ProfilePoint> { new(0, 10), new(length / 2, 14), new(length, 11) };

        var profile = Curves(ProfileGeometry(Parse(Build(segments, pvis: pvis))));

        await Assert.That(profile.Count).IsEqualTo(2);
        await Assert.That(PointOf(profile[0]!, Point2DKey, "position").Y).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task BuildFromSegments_WritesEveryFloatWithADecimalPoint()
    {
        var json = Build(WithSpirals());

        var reader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        var property = string.Empty;
        var offenders = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
            {
                property = reader.GetString();
            }
            else if (reader.TokenType == System.Text.Json.JsonTokenType.Number && property != "type")
            {
                var text = System.Text.Encoding.UTF8.GetString(reader.ValueSpan.ToArray());
                if (!text.Contains('.') && !text.Contains('E') && !text.Contains('e'))
                {
                    offenders.Add($"{property}={text}");
                }
            }
        }

        await Assert.That(offenders.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildFromSegments_CreatesAValidNativeAlignment_WithSpirals()
    {
        var document = NewDocument();
        var internalToShared = document.ActiveProjectLocation.GetTotalTransform().Inverse;
        var segments = WithSpirals();
        var json = AlignmentJsonBuilder.BuildFromSegments("Spiral road", segments, internalToShared, 0.0, 0.0, null, 0.0, 0.005, out var length);

        var id = AlignmentApi.Create(document, "Spiral road", json, length);

        await Assert.That(id).IsNotEqualTo(ElementId.InvalidElementId);
        var info = AlignmentApi.GetInfo(document.GetElement(id));
        await Assert.That(info.Name).IsEqualTo("Spiral road");
        await Assert.That(info.EndStation).IsGreaterThan(info.StartStation);
    }
}

public class ProfileSamplerSegmentTests : TopoAlignTest
{
    private static TriangleSurface Slope()
    {
        // z = 0.1 x over a large square
        var surface = new TriangleSurface();
        surface.AddTriangle(-1000, -1000, -100, 1000, -1000, 100, 1000, 1000, 100);
        surface.AddTriangle(-1000, -1000, -100, 1000, 1000, 100, -1000, 1000, -100);
        return surface;
    }

    [Test]
    public async Task Sample_Segments_AgreeWithTheCurveOverload()
    {
        var curves = new Curve[] { Ln(0, 0, 100, 0), Ln(100, 0, 100, 50) };
        var fromCurves = ProfileSampler.Sample(curves, Slope(), 20, 0);

        var fromSegments = ProfileSampler.Sample(PlanSegmentAdapter.FromCurves(curves), Slope(), 20, 0);

        await Assert.That(fromSegments.Samples.Count).IsEqualTo(fromCurves.Samples.Count);
        for (var i = 0; i < fromCurves.Samples.Count; i++)
        {
            await Assert.That(fromSegments.Samples[i].Station).IsEqualTo(fromCurves.Samples[i].Station).Within(1e-9);
            await Assert.That(fromSegments.Samples[i].Elevation).IsEqualTo(fromCurves.Samples[i].Elevation).Within(1e-9);
        }
    }

    [Test]
    public async Task Sample_SegmentsWithSpirals_ReachTheFullLength()
    {
        var segments = PiGeometry.Create(new[] { new PiPoint(0, 0, 0, 0), new PiPoint(500, 0, 300, 80), new PiPoint(900, 400, 0, 0) }).Segments;

        var result = ProfileSampler.Sample(segments, Slope(), 25, 0);

        await Assert.That(result.Samples[result.Samples.Count - 1].Station).IsEqualTo(PlanSegment.TotalLength(segments)).Within(1e-6);
        await Assert.That(result.Samples.Count).IsGreaterThan(20);
    }

    [Test]
    public async Task Sample_Segments_NullOrEmpty_Throws()
    {
        await Assert.That(Catch(() => ProfileSampler.Sample((IReadOnlyList<PlanSegment>)null, Slope(), 10, 0)) is ArgumentException).IsTrue();
        await Assert.That(Catch(() => ProfileSampler.Sample(new List<PlanSegment>(), Slope(), 10, 0)) is ArgumentException).IsTrue();
    }
}

public class CadCurveReaderTests : TopoAlignTest
{
    [Test]
    public async Task ToCurves_LineAndArc_AreReturnedAsTheyAre()
    {
        await Assert.That(CadCurveReader.ToCurves(Ln(0, 0, 10, 0), out var u1).Count).IsEqualTo(1);
        await Assert.That(u1).IsNull();
        await Assert.That(CadCurveReader.ToCurves(QuarterArcCcw(5), out var u2).Count).IsEqualTo(1);
        await Assert.That(u2).IsNull();
    }

    [Test]
    public async Task ToCurves_PolyLine_BecomesOneLinePerSegment()
    {
        var polyline = PolyLine.Create(new List<XYZ> { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 10, 0) });

        var curves = CadCurveReader.ToCurves(polyline, out var unsupported);

        await Assert.That(unsupported).IsNull();
        await Assert.That(curves.Count).IsEqualTo(3);
        await Assert.That(curves.All(c => c is Line)).IsTrue();
        await Assert.That(curves[2].GetEndPoint(1).X).IsEqualTo(0.0).Within(1e-9);
    }

    [Test]
    public async Task ToCurves_PolyLineWithRepeatedPoints_SkipsTheZeroLengthSegments()
    {
        var polyline = PolyLine.Create(new List<XYZ> { new(0, 0, 0), new(0, 0, 0), new(10, 0, 0), new(10, 0, 0), new(10, 5, 0) });

        var curves = CadCurveReader.ToCurves(polyline, out _);

        await Assert.That(curves.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ToCurves_Ellipse_IsReportedAsUnsupported()
    {
        var ellipse = Ellipse.CreateCurve(XYZ.Zero, 10, 5, XYZ.BasisX, XYZ.BasisY, 0, Math.PI);

        var curves = CadCurveReader.ToCurves(ellipse, out var unsupported);

        await Assert.That(curves.Count).IsEqualTo(0);
        await Assert.That(unsupported).IsEqualTo("ellipse");
    }

    [Test]
    public async Task ToCurves_Null_GivesNothing()
    {
        var curves = CadCurveReader.ToCurves(null, out var unsupported);

        await Assert.That(curves.Count).IsEqualTo(0);
        await Assert.That(unsupported).IsNull();
    }

    [Test]
    public async Task DistanceInPlan_IgnoresHeight_AndFindsTheNearestCurve()
    {
        var curves = new Curve[] { Ln(0, 0, 100, 0, 50), Ln(0, 30, 100, 30, -20) };

        var distance = CadCurveReader.DistanceInPlan(curves, new XYZ(40, 10, 999));

        await Assert.That(distance).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task DistanceInPlan_BeyondTheEnd_MeasuresToTheEndPoint()
    {
        var distance = CadCurveReader.DistanceInPlan(new Curve[] { Ln(0, 0, 10, 0) }, new XYZ(13, 4, 0));

        await Assert.That(distance).IsEqualTo(5.0).Within(1e-9);
    }

    [Test]
    public async Task DistanceInPlan_OnAnArc_UsesTheTessellation()
    {
        var distance = CadCurveReader.DistanceInPlan(new Curve[] { QuarterArcCcw(10) }, new XYZ(7.0710678, 7.0710678, 0));

        await Assert.That(distance).IsLessThan(0.1);
    }

    [Test]
    public async Task PickFilter_AcceptsOnlyImportInstances()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        var filter = new CadCurvePickFilter();

        await Assert.That(filter.AllowElement(line)).IsFalse();
        await Assert.That(filter.AllowReference(null, null)).IsTrue();
    }

    [Test]
    public async Task Read_ElementThatIsNotCad_IsExplained()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var reference = new Reference(line);

        var curves = CadCurveReader.Read(document, new[] { reference }, out var error);

        await Assert.That(curves.Count).IsEqualTo(0);
        await Assert.That(error).IsNotNull();
        await Assert.That(error.Contains("CAD")).IsTrue();
    }
}

public class CreateAlignmentInputTests : TopoAlignUiTest
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

    private CreateAlignmentViewModel NewViewModel()
    {
        App.RevitDocument = NewDocument();
        var viewModel = new CreateAlignmentViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
        return viewModel;
    }

    private static void SetChain(CreateAlignmentViewModel viewModel, IEnumerable<Curve> curves)
    {
        var list = curves.ToList();
        typeof(CreateAlignmentViewModel).GetField("_orderedCurves", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, list);
        viewModel.SelectedCurveCount = list.Count;
        viewModel.IsChainContinuous = true;
    }

    private static Curve[] CornerChain()
    {
        // 450 ft east, a 50 ft radius left-hand quarter turn centred on (450, 50), then 450 ft north
        return new Curve[]
        {
            Ln(0, 0, 450, 0),
            ArcThrough(450, 0, 450 + 50 * Math.Sqrt(0.5), 50 - 50 * Math.Sqrt(0.5), 500, 50),
            Ln(500, 50, 500, 500)
        };
    }

    private static void AddCorner(CreateAlignmentViewModel viewModel, decimal radius = 50m, decimal spiral = 0m)
    {
        viewModel.DefaultRadius = radius;
        viewModel.AddSpirals = spiral > 0m;
        viewModel.SpiralLength = spiral;
        viewModel.AddPoint(0, 0, 0);
        viewModel.AddPoint(500, 0, 0);
        viewModel.AddPoint(500, 500, 0);
    }

    // ---- input source -------------------------------------------------------------------------------------------

    [Test]
    public async Task Constructor_StartsWithLinesAsTheInput()
    {
        using var backup = new SettingsBackup();

        var viewModel = NewViewModel();

        await Assert.That(viewModel.InputSource).IsEqualTo(AlignmentInputSource.Lines);
        await Assert.That(viewModel.IsLinesInput).IsTrue();
        await Assert.That(viewModel.IsPointsInput).IsFalse();
        await Assert.That(viewModel.IsCadInput).IsFalse();
        await Assert.That(viewModel.IsCurveInput).IsTrue();
        await Assert.That(viewModel.AddSpirals).IsFalse();
        await Assert.That(viewModel.PiPoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InputSource_RadioPropertiesStayInStep_AndRaiseChangeNotifications()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IsPointsInput = true;

        await Assert.That(viewModel.InputSource).IsEqualTo(AlignmentInputSource.Points);
        await Assert.That(viewModel.IsLinesInput).IsFalse();
        await Assert.That(viewModel.IsCurveInput).IsFalse();
        await Assert.That(changed.Contains("IsPointsInput")).IsTrue();
        await Assert.That(changed.Contains("IsLinesInput")).IsTrue();
        await Assert.That(changed.Contains("IsCurveInput")).IsTrue();

        viewModel.IsCadInput = true;
        await Assert.That(viewModel.InputSource).IsEqualTo(AlignmentInputSource.Cad);
        viewModel.IsLinesInput = true;
        await Assert.That(viewModel.InputSource).IsEqualTo(AlignmentInputSource.Lines);
    }

    [Test]
    public async Task InputSource_SettingARadioToFalse_DoesNothing()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();

        viewModel.IsPointsInput = false;

        await Assert.That(viewModel.InputSource).IsEqualTo(AlignmentInputSource.Lines);
    }

    [Test]
    public async Task InputSource_ChangingClearsTheStaleProfilePreview()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.ProfilePreview = "old";

        viewModel.IsPointsInput = true;

        await Assert.That(viewModel.ProfilePreview).IsEqualTo(string.Empty);
    }

    // ---- points -------------------------------------------------------------------------------------------------

    [Test]
    public async Task AddPoint_NumbersTheRowsAndShowsTheLocation()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();

        viewModel.AddPoint(0, 0, 0);
        viewModel.AddPoint(100.5, 200, 0);

        await Assert.That(viewModel.PiPoints.Count).IsEqualTo(2);
        await Assert.That(viewModel.PiPoints[0].Number).IsEqualTo(1);
        await Assert.That(viewModel.PiPoints[1].Number).IsEqualTo(2);
        await Assert.That(viewModel.PiPoints[1].Location.Contains("100")).IsTrue();
    }

    [Test]
    public async Task AddPoint_UsesTheDefaultRadius_AndSpiralOnlyWhenEnabled()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.DefaultRadius = 120m;
        viewModel.SpiralLength = 40m;

        viewModel.AddPoint(0, 0, 0);
        viewModel.AddSpirals = true;
        viewModel.AddPoint(100, 0, 0);

        await Assert.That(viewModel.PiPoints[0].Radius).IsEqualTo(120m);
        await Assert.That(viewModel.PiPoints[0].SpiralLength).IsEqualTo(0m);
        await Assert.That(viewModel.PiPoints[1].SpiralLength).IsEqualTo(40m);
    }

    [Test]
    public async Task AddPoint_RepeatOfThePreviousPoint_IsIgnored()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();

        viewModel.AddPoint(10, 10, 0);
        viewModel.AddPoint(10, 10, 0);

        await Assert.That(viewModel.PiPoints.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Points_StatusFollowsTheNumberOfPoints()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        await Assert.That(viewModel.PointsStatus).IsEqualTo("No points picked.");

        viewModel.AddPoint(0, 0, 0);
        await Assert.That(viewModel.PointsStatus).IsEqualTo("Pick at least two points.");

        viewModel.AddPoint(100, 0, 0);
        await Assert.That(viewModel.PointsStatus.Contains("2 points")).IsTrue();
    }

    [Test]
    public async Task Points_AnImpossibleCurve_IsReportedAndBlocksCreate()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.IsPointsInput = true;
        viewModel.DefaultRadius = 500m;
        viewModel.AddPoint(0, 0, 0);
        viewModel.AddPoint(100, 0, 0);
        viewModel.AddPoint(100, 100, 0);

        await Assert.That(viewModel.PointsStatus.Contains("overlap") || viewModel.PointsStatus.Contains("too")).IsTrue();
        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Points_EditingARadius_RelaysTheCurveAndUpdatesTheStatus()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.IsPointsInput = true;
        viewModel.DefaultRadius = 500m;
        viewModel.AddPoint(0, 0, 0);
        viewModel.AddPoint(100, 0, 0);
        viewModel.AddPoint(100, 100, 0);
        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsFalse();

        viewModel.PiPoints[1].Radius = 20m;

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();
        await Assert.That(viewModel.PointsStatus.Contains("3 points")).IsTrue();
    }

    [Test]
    public async Task RemoveLastPoint_AndClearPoints_ShrinkTheList()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        AddCorner(viewModel);

        viewModel.RemoveLastPointCommand.Execute(null);
        await Assert.That(viewModel.PiPoints.Count).IsEqualTo(2);

        viewModel.ClearPointsCommand.Execute(null);
        await Assert.That(viewModel.PiPoints.Count).IsEqualTo(0);
        await Assert.That(viewModel.PointsStatus).IsEqualTo("No points picked.");

        viewModel.RemoveLastPointCommand.Execute(null); // nothing to remove: must not throw
    }

    [Test]
    public async Task Create_InPointsMode_NeedsAValidPointList()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.IsPointsInput = true;
        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsFalse();

        AddCorner(viewModel);

        await Assert.That(viewModel.CreateAlignmentCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Create_InPointsMode_BuildsTheCurvedAlignment()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.IsPointsInput = true;
        viewModel.AlignmentName = "PI road";
        AddCorner(viewModel);
        var closed = 0;
        viewModel.ClosingRequest += (_, _) => closed++;

        viewModel.CreateAlignmentCommand.Execute(null);

        await Assert.That(closed).IsEqualTo(1);
        var request = viewModel.Request;
        await Assert.That(request.Name).IsEqualTo("PI road");
        // two 450 ft straights (500 ft legs less the 50 ft tangent length) plus a 50 ft radius quarter turn
        await Assert.That(request.PlanLength).IsEqualTo(450 + 450 + Math.PI * 25).Within(1e-3);
        var curves = Curves(HorizontalGeometry(Parse(request.Json)));
        await Assert.That(curves.Count).IsEqualTo(3);
        await Assert.That(TypeId(curves[1]!)).IsEqualTo(CircleTypeId);
    }

    [Test]
    public async Task Create_InPointsMode_WithSpirals_WritesTransitionStraights()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.IsPointsInput = true;
        AddCorner(viewModel, radius: 100m, spiral: 40m);

        viewModel.CreateAlignmentCommand.Execute(null);

        var curves = Curves(HorizontalGeometry(Parse(viewModel.Request.Json)));
        await Assert.That(curves.Count).IsGreaterThan(5);
        await Assert.That(curves.Count(c => TypeId(c!) == CircleTypeId)).IsEqualTo(1);
    }

    [Test]
    public async Task Create_InPointsMode_ReverseDirectionStartsAtTheOtherEnd()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.IsPointsInput = true;
        AddCorner(viewModel);
        viewModel.ReverseDirection = true;

        viewModel.CreateAlignmentCommand.Execute(null);

        var first = PointOf(Curves(HorizontalGeometry(Parse(viewModel.Request.Json)))[0]!, Point2DKey, "position");
        await Assert.That(first.X).IsEqualTo(500.0).Within(1e-6);
        await Assert.That(first.Y).IsEqualTo(500.0).Within(1e-6);
    }

    [Test]
    public async Task Create_InPointsMode_FlatProfileTakesTheElevationOfTheFirstPoint()
    {
        using var backup = new SettingsBackup();
        var document = NewDocument();
        App.RevitDocument = document;
        var viewModel = new CreateAlignmentViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
        viewModel.IsPointsInput = true;
        viewModel.DefaultRadius = 50m;
        viewModel.AddPoint(0, 0, 7.5);
        viewModel.AddPoint(500, 0, 0);
        viewModel.AddPoint(500, 500, 0);

        viewModel.CreateAlignmentCommand.Execute(null);

        var expected = document.ActiveProjectLocation.GetTotalTransform().Inverse.OfPoint(new XYZ(0, 0, 7.5)).Z;
        var profile = Curves(ProfileGeometry(Parse(viewModel.Request.Json)));
        await Assert.That(PointOf(profile[0]!, Point2DKey, "position").Y).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task ChangingTheUnits_RefreshesThePointLocations()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        viewModel.AddPoint(100, 0, 0);
        var inFeet = viewModel.PiPoints[0].Location;

        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Meters);

        await Assert.That(viewModel.PiPoints[0].Location).IsNotEqualTo(inFeet);
    }

    // ---- spirals on picked lines --------------------------------------------------------------------------------

    [Test]
    public async Task Create_WithSpiralsOnPickedLines_InsertsTransitionsAndKeepsTheEnds()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, CornerChain());
        viewModel.AddSpirals = true;
        viewModel.SpiralLength = 30m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var curves = Curves(HorizontalGeometry(Parse(viewModel.Request.Json)));
        await Assert.That(curves.Count).IsGreaterThan(5);
        var last = curves[curves.Count - 1]!;
        var position = PointOf(last, Point2DKey, "position");
        var direction = PointOf(last, Vector2DKey, "direction");
        await Assert.That(position.X + direction.X).IsEqualTo(500.0).Within(1e-4);
        await Assert.That(position.Y + direction.Y).IsEqualTo(500.0).Within(1e-4);
    }

    [Test]
    public async Task Create_WithoutSpirals_KeepsThePickedCurvesExactly()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, CornerChain());

        viewModel.CreateAlignmentCommand.Execute(null);

        var curves = Curves(HorizontalGeometry(Parse(viewModel.Request.Json)));
        await Assert.That(curves.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Preview_SpiralsOnAChainThatStartsWithAnArc_ExplainsWhy()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, new Curve[] { QuarterArcCcw(50), Ln(0, 50, 0, 150) });
        viewModel.AddSpirals = true;
        viewModel.SpiralLength = 20m;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("straight")).IsTrue();
    }

    [Test]
    public async Task Preview_SpiralLengthOfZero_AsksForALength()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, CornerChain());
        viewModel.AddSpirals = true;
        viewModel.SpiralLength = 0m;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("spiral length")).IsTrue();
    }

    [Test]
    public async Task Preview_SpiralsThatDoNotFit_AreExplained()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, CornerChain());
        viewModel.AddSpirals = true;
        viewModel.SpiralLength = 500m;

        viewModel.PreviewProfileCommand.Execute(null);

        await Assert.That(viewModel.ProfilePreview.Contains("too long") || viewModel.ProfilePreview.Contains("overlap")).IsTrue();
    }

    // ---- picking ------------------------------------------------------------------------------------------------

    [Test]
    public async Task PickPoints_RequestsAPointPickAndHidesTheWindow()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickPointsCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(viewModel.PickPointsRequested).IsTrue();
        await Assert.That(viewModel.PickCadRequested).IsFalse();
        await Assert.That(viewModel.PickToposolidRequested).IsFalse();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task PickCad_RequestsACadPickAndHidesTheWindow()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickCadCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(viewModel.PickCadRequested).IsTrue();
        await Assert.That(viewModel.PickPointsRequested).IsFalse();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task EachPickCommand_ClearsTheOtherPickKinds()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();

        viewModel.PickPointsCommand.Execute(null);
        viewModel.PickLinesCommand.Execute(null);
        await Assert.That(viewModel.PickPointsRequested).IsFalse();

        viewModel.PickCadCommand.Execute(null);
        viewModel.PickToposolidCommand.Execute(null);
        await Assert.That(viewModel.PickCadRequested).IsFalse();
        await Assert.That(viewModel.PickToposolidRequested).IsTrue();
    }

    // ---- settings -----------------------------------------------------------------------------------------------

    [Test]
    public async Task Settings_SpiralAndRadiusDefaults()
    {
        using var backup = new SettingsBackup();

        var settings = new Settings();

        await Assert.That(settings.CreateAlignmentAddSpirals).IsFalse();
        await Assert.That((double)settings.CreateAlignmentSpiralLength).IsEqualTo(30.0 / 0.3048).Within(1e-9);
        await Assert.That((double)settings.CreateAlignmentDefaultRadius).IsEqualTo(100.0 / 0.3048).Within(1e-9);
    }

    [Test]
    public async Task Settings_SpiralOptionsRoundTripThroughTheSettingsFile()
    {
        using var backup = new SettingsBackup();
        new Settings { CreateAlignmentAddSpirals = true, CreateAlignmentSpiralLength = 75m, CreateAlignmentDefaultRadius = 410m }.SaveSettings();

        var loaded = new Settings();
        loaded.LoadSettings();

        await Assert.That(loaded.CreateAlignmentAddSpirals).IsTrue();
        await Assert.That(loaded.CreateAlignmentSpiralLength).IsEqualTo(75m);
        await Assert.That(loaded.CreateAlignmentDefaultRadius).IsEqualTo(410m);
    }

    [Test]
    public async Task Create_SavesTheSpiralOptions()
    {
        using var backup = new SettingsBackup();
        var viewModel = NewViewModel();
        SetChain(viewModel, CornerChain());
        viewModel.AddSpirals = true;
        viewModel.SpiralLength = 25m;
        viewModel.DefaultRadius = 80m;

        viewModel.CreateAlignmentCommand.Execute(null);

        var saved = new Settings();
        saved.LoadSettings();
        await Assert.That(saved.CreateAlignmentAddSpirals).IsTrue();
        await Assert.That((double)saved.CreateAlignmentSpiralLength).IsEqualTo(25.0).Within(1e-6);
        await Assert.That((double)saved.CreateAlignmentDefaultRadius).IsEqualTo(80.0).Within(1e-6);
    }

    // ---- the window ---------------------------------------------------------------------------------------------

    [Test]
    public async Task CreateAlignmentView_HasTheInputSourceControls()
    {
        using var backup = new SettingsBackup();
        App.RevitDocument = NewDocument();
        var viewModel = new CreateAlignmentViewModel();
        var buttons = new List<string>();
        var radios = new List<string>();

        RunOnStaThread(() =>
        {
            var view = new CreateAlignmentView(viewModel);
            Walk(view, element =>
            {
                if (element is System.Windows.Controls.Button button)
                {
                    buttons.Add(button.Content?.ToString());
                }
                else if (element is System.Windows.Controls.RadioButton radio)
                {
                    radios.Add(radio.Content?.ToString());
                }
            });
        });

        await Assert.That(buttons.Contains("Pick lines/arcs")).IsTrue();
        await Assert.That(buttons.Contains("Pick points")).IsTrue();
        await Assert.That(buttons.Any(b => b != null && b.StartsWith("Pick CAD"))).IsTrue();
        await Assert.That(buttons.Contains("Remove last point")).IsTrue();
        await Assert.That(buttons.Contains("Clear points")).IsTrue();
        await Assert.That(radios.Count).IsEqualTo(3);
    }

    [Test]
    public async Task CreateAlignmentView_HidesItselfWhenAPointPickIsRequested()
    {
        using var backup = new SettingsBackup();
        App.RevitDocument = NewDocument();
        var viewModel = new CreateAlignmentViewModel();
        System.Windows.Visibility visibility = System.Windows.Visibility.Visible;

        RunOnStaThread(() =>
        {
            var view = new CreateAlignmentView(viewModel);
            viewModel.PickPointsCommand.Execute(null);
            visibility = view.Visibility;
        });

        await Assert.That(visibility).IsEqualTo(System.Windows.Visibility.Hidden);
    }

    private static void Walk(DependencyObject parent, Action<DependencyObject> visit)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is DependencyObject element)
            {
                visit(element);
                Walk(element, visit);
            }
        }
    }
}
