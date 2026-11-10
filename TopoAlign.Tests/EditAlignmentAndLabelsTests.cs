using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Alignments.AlignmentJson;
using TopoAlign.Alignments.Civil;

namespace TopoAlign.Tests;

internal static class ViewHelpers
{
    public static ViewPlan PlanView(Document document)
    {
        return new FilteredElementCollector(document).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
            .First(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan);
    }

    public static View3D NewIsometricView(Document document)
    {
        using var transaction = new Transaction(document, "3D view");
        transaction.Start();
        var type = new FilteredElementCollector(document).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(t => t.ViewFamily == ViewFamily.ThreeDimensional);
        var view = View3D.CreateIsometric(document, type.Id);
        transaction.Commit();
        return view;
    }
}

public class AlignmentApiUpdateTests : TopoAlignTest
{
    private static (Document Document, ElementId Id) NewAlignment(AlignmentApiUpdateTests test, string name = "Road 1")
    {
        var document = test.CreateDocument();
        return (document, CreateAlignment(document, name));
    }

    internal Document CreateDocument() => NewDocument();

    [Test]
    public async Task Update_NullArguments_Throw()
    {
        var (document, id) = NewAlignment(this);
        var element = document.GetElement(id);
        var json = AlignmentApi.GetJson(element);

        await Assert.That(Catch(() => AlignmentApi.Update(null, element, json, 1)) is ArgumentNullException).IsTrue();
        await Assert.That(Catch(() => AlignmentApi.Update(document, null, json, 1)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Update_BlankJson_Throws()
    {
        var (document, id) = NewAlignment(this);
        var element = document.GetElement(id);

        await Assert.That(Catch(() => AlignmentApi.Update(document, element, "  ", 1)) is ArgumentException).IsTrue();
        await Assert.That(Catch(() => AlignmentApi.Update(document, element, null, 1)) is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Update_Rename_ChangesTheAlignmentsName()
    {
        var (document, id) = NewAlignment(this, "Before");
        var element = document.GetElement(id);
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        edit.Name = "After";

        AlignmentApi.Update(document, element, edit.ToJson(), edit.PlanLength, "After");

        var info = AlignmentApi.GetInfo(document.GetElement(id));
        await Assert.That(info.Name).IsEqualTo("After");
    }

    [Test]
    public async Task Update_Rename_AlsoNamesTheAlignmentType()
    {
        var (document, id) = NewAlignment(this, "Before");
        var element = document.GetElement(id);
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        edit.Name = "Type name";

        AlignmentApi.Update(document, element, edit.ToJson(), edit.PlanLength, "Type name");

        var type = document.GetElement(document.GetElement(id).GetTypeId());
        await Assert.That(type.Name).IsEqualTo("Type name");
    }

    [Test]
    public async Task Update_StartStation_ShiftsTheStationsButNotTheLength()
    {
        var (document, id) = NewAlignment(this);
        var element = document.GetElement(id);
        var before = AlignmentApi.GetInfo(element);
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        edit.StartStation = 500.0;

        AlignmentApi.Update(document, element, edit.ToJson(), edit.PlanLength);

        var after = AlignmentApi.GetInfo(document.GetElement(id));
        await Assert.That(after.StartStation).IsGreaterThan(before.StartStation);
        await Assert.That(after.EndStation - after.StartStation).IsEqualTo(before.EndStation - before.StartStation).Within(1e-6);
    }

    [Test]
    public async Task Update_Reverse_SwapsTheEnds()
    {
        var (document, id) = NewAlignment(this);
        var element = document.GetElement(id);
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        edit.Reverse();

        AlignmentApi.Update(document, element, edit.ToJson(), edit.PlanLength);

        var reversed = document.GetElement(id);
        var info = AlignmentApi.GetInfo(reversed);
        // the alignment used to run (0,0) to (100,50); now it starts at the old end
        var start = AlignmentApi.GetPointAtStation(reversed, info.StartStation);
        var end = AlignmentApi.GetPointAtStation(reversed, info.EndStation);
        await Assert.That(start.X).IsEqualTo(100.0).Within(1e-3);
        await Assert.That(start.Y).IsEqualTo(50.0).Within(1e-3);
        await Assert.That(end.X).IsEqualTo(0.0).Within(1e-3);
        await Assert.That(end.Y).IsEqualTo(0.0).Within(1e-3);
    }

    [Test]
    public async Task Update_KeepsTheSameElement()
    {
        var (document, id) = NewAlignment(this);
        var element = document.GetElement(id);
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        edit.Name = "Same element";

        AlignmentApi.Update(document, element, edit.ToJson(), edit.PlanLength, "Same element");

        await Assert.That(AlignmentApi.GetAlignmentElements(document).Count).IsEqualTo(1);
        await Assert.That(AlignmentApi.IsValid(document.GetElement(id))).IsTrue();
    }
}

public class StationLabelApiTests : TopoAlignTest
{
    [Test]
    public async Task CanCreateStationLabels_WhenRevitShipsCivilAlignments_IsTrue()
    {
        await Assert.That(AlignmentApi.CanCreateStationLabels).IsTrue();
    }

    [Test]
    public async Task GetStationLabelTypes_NullDocument_IsEmpty()
    {
        await Assert.That(AlignmentApi.GetStationLabelTypes(null).Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateStationLabels_NullArguments_Throw()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);

        await Assert.That(Catch(() => AlignmentApi.CreateStationLabels(null, element, view, null, null, null, null, null, true)) is ArgumentNullException).IsTrue();
        await Assert.That(Catch(() => AlignmentApi.CreateStationLabels(document, null, view, null, null, null, null, null, true)) is ArgumentNullException).IsTrue();
        await Assert.That(Catch(() => AlignmentApi.CreateStationLabels(document, element, null, null, null, null, null, null, true)) is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task CreateStationLabels_WithoutAType_CreatesTheRecommendedTypeAndTheLabels()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);

        var count = AlignmentApi.CreateStationLabels(document, element, view, null, null, null, 25.0, null, true);

        await Assert.That(count).IsGreaterThanOrEqualTo(6);
        await Assert.That(count).IsLessThanOrEqualTo(7);
        await Assert.That(AlignmentApi.GetStationLabelTypes(document).Any(t => t.IsRecommended)).IsTrue();
    }

    [Test]
    public async Task CreateStationLabels_ExistingRecommendedType_IsReused()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);
        AlignmentApi.CreateStationLabels(document, element, view, null, null, null, 50.0, null, true);
        var types = AlignmentApi.GetStationLabelTypes(document);

        AlignmentApi.CreateStationLabels(document, element, view, types.First(t => t.IsRecommended).Id, null, null, 50.0, null, false);

        await Assert.That(AlignmentApi.GetStationLabelTypes(document).Count).IsEqualTo(types.Count);
    }

    [Test]
    public async Task CreateStationLabels_AStationRange_LimitsTheLabels()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);
        var info = AlignmentApi.GetInfo(element);

        var all = AlignmentApi.CreateStationLabels(document, element, view, null, null, null, 10.0, null, true);
        var types = AlignmentApi.GetStationLabelTypes(document);
        var some = AlignmentApi.CreateStationLabels(document, element, view, types[0].Id, info.StartStation, info.StartStation + 40.0, 10.0, null, true);

        await Assert.That(some).IsLessThan(all);
        await Assert.That(some).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task CreateStationLabels_StartAfterEnd_IsRejected()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);
        var info = AlignmentApi.GetInfo(element);

        var exception = Catch(() => AlignmentApi.CreateStationLabels(document, element, view, null, info.StartStation + 100, info.StartStation + 10, 10.0, null, true));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task CreateStationLabels_AnIntervalThatIsTooSmall_IsRejected()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);

        var exception = Catch(() => AlignmentApi.CreateStationLabels(document, element, view, null, null, null, 0.5, null, true));

        await Assert.That(exception is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task CreateStationLabels_ARejectedRequest_LeavesNoNewTypeBehind()
    {
        var document = NewDocument();
        var element = document.GetElement(CreateAlignment(document));
        var view = ViewHelpers.PlanView(document);
        var before = AlignmentApi.GetStationLabelTypes(document).Count;

        Catch(() => AlignmentApi.CreateStationLabels(document, element, view, null, null, null, 0.5, null, true));

        await Assert.That(AlignmentApi.GetStationLabelTypes(document).Count).IsEqualTo(before);
    }
}

public class EditAlignmentViewModelTests : TopoAlignUiTest
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

    private (EditAlignmentViewModel ViewModel, Document Document, Element Alignment) NewViewModel()
    {
        var document = NewDocument();
        App.RevitDocument = document;
        var alignment = document.GetElement(CreateAlignment(document, "Road 1"));
        var viewModel = new EditAlignmentViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
        return (viewModel, document, alignment);
    }

    [Test]
    public async Task Constructor_StartsWithNothingSelected()
    {
        App.RevitDocument = NewDocument();

        var viewModel = new EditAlignmentViewModel();

        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.AlignmentStatus).IsEqualTo("No alignment selected.");
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.DisplayUnits.Count).IsGreaterThan(0);
        await Assert.That(viewModel.SelectedDisplayUnit).IsNotNull();
        await Assert.That(viewModel.WindowTitle.StartsWith("Edit Alignment ")).IsTrue();
        await Assert.That(viewModel.Request).IsNull();
        await Assert.That(viewModel.PickRequested).IsFalse();
    }

    [Test]
    public async Task SelectAlignment_NotAnAlignment_IsRejected()
    {
        var (viewModel, document, _) = NewViewModel();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(viewModel.SelectAlignment(line)).IsFalse();
        await Assert.That(viewModel.SelectAlignment(null)).IsFalse();
        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.AlignmentStatus).IsEqualTo("That element is not an alignment.");
    }

    [Test]
    public async Task SelectAlignment_ReadsNameStartStationAndLength()
    {
        var (viewModel, _, alignment) = NewViewModel();

        var selected = viewModel.SelectAlignment(alignment);

        await Assert.That(selected).IsTrue();
        await Assert.That(viewModel.HasAlignment).IsTrue();
        await Assert.That(viewModel.AlignmentName).IsEqualTo("Road 1");
        await Assert.That(viewModel.StartStation).IsEqualTo(0m);
        await Assert.That(viewModel.CanReverse).IsTrue();
        await Assert.That(viewModel.ReverseDirection).IsFalse();
        await Assert.That(viewModel.AlignmentStatus.Contains("Road 1")).IsTrue();
        await Assert.That(viewModel.AlignmentStatus.Contains("150.00")).IsTrue();
    }

    [Test]
    public async Task SelectAlignment_ShowsTheStartStationOfAnAlignmentThatDoesNotStartAtZero()
    {
        var (viewModel, document, _) = NewViewModel();
        var shifted = document.GetElement(CreateAlignment(document, "Shifted", startStation: 1250.5));

        viewModel.SelectAlignment(shifted);

        await Assert.That(viewModel.StartStation).IsEqualTo(1250.5m);
    }

    [Test]
    public async Task Apply_IsDisabledUntilSomethingChanges()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();

        viewModel.AlignmentName = "Renamed";
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsTrue();

        viewModel.AlignmentName = "Road 1";
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();

        viewModel.ReverseDirection = true;
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Apply_IsDisabledForABlankName()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.ReverseDirection = true;

        viewModel.AlignmentName = "   ";

        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Apply_IgnoresTinyStartStationNoise_ButNoticesRealChanges()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);

        viewModel.StartStation += 0.0004m;
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();

        viewModel.StartStation += 1m;
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Apply_BuildsTheRequestAndClosesTheDialog()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.AlignmentName = "  New name  ";
        viewModel.StartStation = 2000m;
        viewModel.ReverseDirection = true;
        var closed = 0;
        viewModel.ClosingRequest += (_, _) => closed++;

        viewModel.ApplyCommand.Execute(null);

        await Assert.That(closed).IsEqualTo(1);
        var request = viewModel.Request;
        await Assert.That(request.Name).IsEqualTo("New name");
        await Assert.That(request.Renamed).IsTrue();
        await Assert.That(request.StartStationChanged).IsTrue();
        await Assert.That(request.Reversed).IsTrue();
        await Assert.That(request.PlanLength).IsEqualTo(150.0).Within(1e-6);
        var edited = AlignmentJsonDocument.Parse(request.Json);
        await Assert.That(edited.Name).IsEqualTo("New name");
        await Assert.That(edited.StartStation).IsEqualTo(2000.0).Within(1e-9);
    }

    [Test]
    public async Task Apply_OnlyRenaming_LeavesTheGeometryAlone()
    {
        var (viewModel, _, alignment) = NewViewModel();
        var original = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(alignment));
        viewModel.SelectAlignment(alignment);
        viewModel.AlignmentName = "Just a name";

        viewModel.ApplyCommand.Execute(null);

        var request = viewModel.Request;
        await Assert.That(request.Renamed).IsTrue();
        await Assert.That(request.StartStationChanged).IsFalse();
        await Assert.That(request.Reversed).IsFalse();
        await Assert.That(AlignmentJsonDocument.Parse(request.Json).StartStation).IsEqualTo(original.StartStation);
    }

    [Test]
    public async Task ChangingTheUnits_KeepsTheSamePhysicalStartStation()
    {
        var (viewModel, document, _) = NewViewModel();
        var shifted = document.GetElement(CreateAlignment(document, "Shifted", startStation: 100.0));
        viewModel.SelectAlignment(shifted);

        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Meters);

        await Assert.That((double)viewModel.StartStation).IsEqualTo(30.48).Within(0.001);
        // switching units is not an edit
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task PickAlignment_RequestsAPickAndHidesTheWindow()
    {
        var (viewModel, _, _) = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickAlignmentCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task EndToEnd_AppliedRequestUpdatesTheAlignmentInTheModel()
    {
        var (viewModel, document, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.AlignmentName = "Edited in the dialog";
        viewModel.ReverseDirection = true;
        viewModel.ApplyCommand.Execute(null);
        var request = viewModel.Request;

        AlignmentApi.Update(document, request.Alignment, request.Json, request.PlanLength, request.Name);

        var info = AlignmentApi.GetInfo(document.GetElement(alignment.Id));
        await Assert.That(info.Name).IsEqualTo("Edited in the dialog");
        var start = AlignmentApi.GetPointAtStation(document.GetElement(alignment.Id), info.StartStation);
        await Assert.That(start.X).IsEqualTo(100.0).Within(1e-3);
    }
}

public class StationLabelsViewModelTests : TopoAlignUiTest
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

    private (StationLabelsViewModel ViewModel, Document Document, Element Alignment) NewViewModel(bool usePlanView = true)
    {
        var document = NewDocument();
        App.RevitDocument = document;
        var alignment = document.GetElement(CreateAlignment(document, "Road 1"));
        var viewModel = new StationLabelsViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);

        if (usePlanView)
        {
            // a document made through the API has no active view, so give the dialog a plan to work with
            typeof(StationLabelsViewModel).GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(viewModel, ViewHelpers.PlanView(document));
        }

        return (viewModel, document, alignment);
    }

    [Test]
    public async Task Constructor_OffersAtLeastTheRecommendedTypeAndSelectsIt()
    {
        App.RevitDocument = NewDocument();

        var viewModel = new StationLabelsViewModel();

        await Assert.That(viewModel.LabelTypes.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(viewModel.SelectedLabelType).IsNotNull();
        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.HasLeader).IsTrue();
        await Assert.That(viewModel.WindowTitle.StartsWith("Station Labels ")).IsTrue();
        await Assert.That(viewModel.Request).IsNull();
    }

    [Test]
    public async Task IsSupported_PlansYes_ThreeDimensionalViewsAndNothingNo()
    {
        var document = NewDocument();

        await Assert.That(StationLabelsViewModel.IsSupported(ViewHelpers.PlanView(document))).IsTrue();
        await Assert.That(StationLabelsViewModel.IsSupported(ViewHelpers.NewIsometricView(document))).IsFalse();
        await Assert.That(StationLabelsViewModel.IsSupported(null)).IsFalse();
    }

    [Test]
    public async Task SelectAlignment_NotAnAlignment_IsRejected()
    {
        var (viewModel, document, _) = NewViewModel();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(viewModel.SelectAlignment(line)).IsFalse();
        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.AlignmentStatus).IsEqualTo("That element is not an alignment.");
    }

    [Test]
    public async Task SelectAlignment_ShowsTheNameAndStationRange()
    {
        var (viewModel, _, alignment) = NewViewModel();

        await Assert.That(viewModel.SelectAlignment(alignment)).IsTrue();

        await Assert.That(viewModel.HasAlignment).IsTrue();
        await Assert.That(viewModel.AlignmentStatus.Contains("Road 1")).IsTrue();
        await Assert.That(viewModel.AlignmentStatus.Contains("stations")).IsTrue();
    }

    [Test]
    public async Task Create_NeedsAnAlignmentAndASupportedView()
    {
        var (viewModel, _, alignment) = NewViewModel();
        await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsFalse();

        viewModel.SelectAlignment(alignment);
        await Assert.That(viewModel.CreateCommand.CanExecute(null)).IsTrue();

        var (noView, _, other) = NewViewModel(usePlanView: false);
        noView.SelectAlignment(other);
        typeof(StationLabelsViewModel).GetField("_view", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(noView, ViewHelpers.NewIsometricView(App.RevitDocument));
        await Assert.That(noView.CreateCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Create_BlankFieldsMeanDefaults()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.IntervalText = string.Empty;
        var closed = 0;
        viewModel.ClosingRequest += (_, _) => closed++;

        viewModel.CreateCommand.Execute(null);

        await Assert.That(closed).IsEqualTo(1);
        var request = viewModel.Request;
        await Assert.That(request.Interval).IsNull();
        await Assert.That(request.Offset).IsNull();
        await Assert.That(request.StartStation).IsNull();
        await Assert.That(request.EndStation).IsNull();
        await Assert.That(request.HasLeader).IsTrue();
        await Assert.That(request.AlignmentName).IsEqualTo("Road 1");
    }

    [Test]
    public async Task SelectAlignment_SuggestsAnIntervalThatGivesSeveralLabels()
    {
        var (viewModel, _, alignment) = NewViewModel();

        viewModel.SelectAlignment(alignment);

        await Assert.That(viewModel.IntervalText).IsNotEqualTo(string.Empty);
        await Assert.That(StationLabelsViewModel.SuggestInterval(137.0)).IsEqualTo(10.ToString());
        await Assert.That(StationLabelsViewModel.SuggestInterval(420.0)).IsEqualTo(50.ToString());
        await Assert.That(StationLabelsViewModel.SuggestInterval(0.0)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Create_ConvertsTheEnteredLengthsToFeet()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.IntervalText = "25";
        viewModel.OffsetText = "-0.25";
        viewModel.StartStationText = "10";
        viewModel.EndStationText = "120";
        viewModel.HasLeader = false;

        viewModel.CreateCommand.Execute(null);

        var request = viewModel.Request;
        await Assert.That(request.Interval.Value).IsEqualTo(25.0).Within(1e-9);
        await Assert.That(request.Offset.Value).IsEqualTo(-0.25).Within(1e-9);
        await Assert.That(request.StartStation.Value).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(request.EndStation.Value).IsEqualTo(120.0).Within(1e-9);
        await Assert.That(request.HasLeader).IsFalse();
    }

    [Test]
    public async Task Create_UsesTheSelectedUnits()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Meters);
        viewModel.IntervalText = "10";

        viewModel.CreateCommand.Execute(null);

        await Assert.That(viewModel.Request.Interval.Value).IsEqualTo(10.0 / 0.3048).Within(1e-6);
    }

    [Test]
    public async Task Create_BadInput_IsExplainedAndNothingIsRequested()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);

        viewModel.IntervalText = "abc";
        viewModel.CreateCommand.Execute(null);
        await Assert.That(viewModel.InputMessage.Contains("not a number")).IsTrue();
        await Assert.That(viewModel.InputMessage.Contains("interval")).IsTrue();

        viewModel.IntervalText = "0";
        viewModel.CreateCommand.Execute(null);
        await Assert.That(viewModel.InputMessage.Contains("greater than zero")).IsTrue();

        viewModel.IntervalText = string.Empty;
        viewModel.StartStationText = "50";
        viewModel.EndStationText = "10";
        viewModel.CreateCommand.Execute(null);
        await Assert.That(viewModel.InputMessage.Contains("start station")).IsTrue();

        await Assert.That(viewModel.Request).IsNull();
    }

    [Test]
    public async Task Create_ClearsAnEarlierMessageOnceTheInputIsGood()
    {
        var (viewModel, _, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.IntervalText = "x";
        viewModel.CreateCommand.Execute(null);
        await Assert.That(viewModel.InputMessage).IsNotEqualTo(string.Empty);

        viewModel.IntervalText = "20";
        viewModel.CreateCommand.Execute(null);

        await Assert.That(viewModel.InputMessage).IsEqualTo(string.Empty);
        await Assert.That(viewModel.Request).IsNotNull();
    }

    [Test]
    public async Task PickAlignment_RequestsAPickAndHidesTheWindow()
    {
        var (viewModel, _, _) = NewViewModel();
        var hidden = 0;
        viewModel.HideRequested += (_, _) => hidden++;

        viewModel.PickAlignmentCommand.Execute(null);

        await Assert.That(viewModel.PickRequested).IsTrue();
        await Assert.That(hidden).IsEqualTo(1);
    }

    [Test]
    public async Task EndToEnd_TheRequestCreatesLabelsInTheView()
    {
        var (viewModel, document, alignment) = NewViewModel();
        viewModel.SelectAlignment(alignment);
        viewModel.IntervalText = "25";
        viewModel.CreateCommand.Execute(null);
        var request = viewModel.Request;

        var count = AlignmentApi.CreateStationLabels(document, request.Alignment, request.View, request.LabelTypeId,
            request.StartStation, request.EndStation, request.Interval, request.Offset, request.HasLeader);

        await Assert.That(count).IsGreaterThanOrEqualTo(6);
    }
}

public class EditAndLabelViewTests : TopoAlignUiTest
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

    private static List<string> ButtonLabels(System.Windows.DependencyObject view)
    {
        var labels = new List<string>();
        void Walk(System.Windows.DependencyObject parent)
        {
            foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(parent))
            {
                if (child is System.Windows.DependencyObject element)
                {
                    if (element is System.Windows.Controls.Button button)
                    {
                        labels.Add(button.Content?.ToString());
                    }

                    Walk(element);
                }
            }
        }

        Walk(view);
        return labels;
    }

    [Test]
    public async Task EditAlignmentView_HasItsButtons_AndHidesWhenAPickIsRequested()
    {
        App.RevitDocument = NewDocument();
        var viewModel = new EditAlignmentViewModel();
        List<string> buttons = null;
        System.Windows.Visibility visibility = System.Windows.Visibility.Visible;

        RunOnStaThread(() =>
        {
            var view = new EditAlignmentView(viewModel);
            buttons = ButtonLabels(view);
            viewModel.PickAlignmentCommand.Execute(null);
            visibility = view.Visibility;
        });

        await Assert.That(buttons.Contains("Pick alignment")).IsTrue();
        await Assert.That(buttons.Contains("Apply")).IsTrue();
        await Assert.That(visibility).IsEqualTo(System.Windows.Visibility.Hidden);
    }

    [Test]
    public async Task StationLabelsView_HasItsButtons_AndHidesWhenAPickIsRequested()
    {
        App.RevitDocument = NewDocument();
        var viewModel = new StationLabelsViewModel();
        List<string> buttons = null;
        System.Windows.Visibility visibility = System.Windows.Visibility.Visible;

        RunOnStaThread(() =>
        {
            var view = new StationLabelsView(viewModel);
            buttons = ButtonLabels(view);
            viewModel.PickAlignmentCommand.Execute(null);
            visibility = view.Visibility;
        });

        await Assert.That(buttons.Contains("Pick alignment")).IsTrue();
        await Assert.That(buttons.Contains("Create Labels")).IsTrue();
        await Assert.That(visibility).IsEqualTo(System.Windows.Visibility.Hidden);
    }
}
