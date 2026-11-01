using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Tests;

/// <summary>Writing an edited profile back to a real alignment through the Civil Alignments API.</summary>
public class AlignmentApiProfileUpdateTests : TopoAlignTest
{
    internal Document CreateDocument() => NewDocument();

    [Test]
    public async Task Update_AddedStation_IsReadBackFromTheAlignment()
    {
        var document = CreateDocument();
        var element = document.GetElement(CreateAlignment(document));
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        var vertices = ProfileEditing.Insert(edit.ReadProfile(), 75.0, 0.0);
        vertices[1] = vertices[1].WithElevation(vertices[1].Elevation + 12.0);
        edit.SetProfile(vertices);

        AlignmentApi.Update(document, element, edit.ToJson(), edit.ProfileLength);

        var after = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(document.GetElement(element.Id))).ReadProfile();
        await Assert.That(after.Count).IsEqualTo(3);
        await Assert.That(after[1].Distance).IsEqualTo(75.0).Within(1e-6);
        await Assert.That(after[1].Elevation).IsEqualTo(vertices[1].Elevation).Within(1e-6);
    }

    [Test]
    public async Task Update_EditedProfile_KeepsTheLengthAndThePlan()
    {
        var document = CreateDocument();
        var element = document.GetElement(CreateAlignment(document));
        var before = AlignmentApi.GetInfo(element);
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        edit.SetProfile(ProfileEditing.Insert(edit.ReadProfile(), 120.0, 20.0));

        AlignmentApi.Update(document, element, edit.ToJson(), edit.ProfileLength);

        var info = AlignmentApi.GetInfo(document.GetElement(element.Id));
        await Assert.That(info.EndStation - info.StartStation).IsEqualTo(before.EndStation - before.StartStation).Within(1e-6);
        var point = AlignmentApi.GetPointAtStation(document.GetElement(element.Id), info.StartStation + 100.0);
        await Assert.That(point.X).IsEqualTo(100.0).Within(1e-3);
        await Assert.That(point.Y).IsEqualTo(0.0).Within(1e-3);
    }

    [Test]
    public async Task Update_EditedProfile_AlignmentPassesThroughTheNewElevation()
    {
        var document = CreateDocument();
        var element = document.GetElement(CreateAlignment(document));
        var edit = AlignmentJsonDocument.Parse(AlignmentApi.GetJson(element));
        var vertices = ProfileEditing.Insert(edit.ReadProfile(), 100.0, 0.0);
        vertices[1] = vertices[1].WithElevation(vertices[1].Elevation + 8.0);
        edit.SetProfile(vertices);

        AlignmentApi.Update(document, element, edit.ToJson(), edit.ProfileLength);

        var updated = document.GetElement(element.Id);
        var info = AlignmentApi.GetInfo(updated);
        var point = AlignmentApi.GetPointAtStation(updated, info.StartStation + 100.0);
        await Assert.That(point.Z).IsEqualTo(vertices[1].Elevation).Within(1e-3);
    }
}

public class EditProfileViewModelTests : TopoAlignUiTest
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

    private (EditProfileViewModel ViewModel, Document Document, Element Alignment) NewViewModel()
    {
        var document = NewDocument();
        App.RevitDocument = document;
        var alignment = document.GetElement(CreateAlignment(document, "Road 1"));
        var viewModel = new EditProfileViewModel();
        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Feet);
        viewModel.SelectAlignment(alignment);
        return (viewModel, document, alignment);
    }

    [Test]
    public async Task Constructor_StartsWithNothingSelected()
    {
        App.RevitDocument = NewDocument();

        var viewModel = new EditProfileViewModel();

        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.Rows.Count).IsEqualTo(0);
        await Assert.That(viewModel.AlignmentStatus).IsEqualTo("No alignment selected.");
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.AddStationCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.WindowTitle.StartsWith("Edit Profile ")).IsTrue();
        await Assert.That(viewModel.Request).IsNull();
    }

    [Test]
    public async Task SelectAlignment_NotAnAlignment_IsRejected()
    {
        var (viewModel, document, _) = NewViewModel();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(viewModel.SelectAlignment(line)).IsFalse();
        await Assert.That(viewModel.SelectAlignment(null)).IsFalse();
        await Assert.That(viewModel.HasAlignment).IsFalse();
        await Assert.That(viewModel.Rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SelectAlignment_LoadsTheStartAndEndStations()
    {
        var (viewModel, _, _) = NewViewModel();

        await Assert.That(viewModel.HasAlignment).IsTrue();
        await Assert.That(viewModel.Rows.Count).IsEqualTo(2);
        await Assert.That(viewModel.Rows[0].Station).IsEqualTo(0m);
        await Assert.That(viewModel.Rows[1].Station).IsEqualTo(150m);
        await Assert.That(viewModel.Rows[0].IsEnd).IsTrue();
        await Assert.That(viewModel.Rows[1].IsEnd).IsTrue();
        await Assert.That(viewModel.Rows[0].CanEditStation).IsFalse();
        await Assert.That(viewModel.AlignmentStatus.Contains("Road 1")).IsTrue();
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task AddStation_InsertsAnOrderedRowOnTheGradeLine()
    {
        var (viewModel, _, _) = NewViewModel();
        viewModel.Rows[1].Elevation = 30m;
        viewModel.NewStation = 50m;

        viewModel.AddStationCommand.Execute(null);

        await Assert.That(viewModel.Rows.Count).IsEqualTo(3);
        await Assert.That(viewModel.Rows[1].Station).IsEqualTo(50m);
        await Assert.That(viewModel.Rows[1].Elevation).IsEqualTo(decimal.Round(viewModel.Rows[0].Elevation + (30m - viewModel.Rows[0].Elevation) / 3m, 3));
        await Assert.That(viewModel.Rows[1].IsEnd).IsFalse();
        await Assert.That(viewModel.SelectedRow).IsEqualTo(viewModel.Rows[1]);
    }

    [Test]
    public async Task AddStation_OutsideTheAlignmentOrOnAnExistingStation_IsRefused()
    {
        var (viewModel, _, _) = NewViewModel();

        viewModel.NewStation = 400m;
        viewModel.AddStationCommand.Execute(null);
        await Assert.That(viewModel.Rows.Count).IsEqualTo(2);

        viewModel.NewStation = 0m;
        viewModel.AddStationCommand.Execute(null);
        await Assert.That(viewModel.Rows.Count).IsEqualTo(2);
        await Assert.That(viewModel.ProfileStatus.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task EditingAnElevation_EnablesApply()
    {
        var (viewModel, _, _) = NewViewModel();
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();

        viewModel.Rows[1].Elevation = viewModel.Rows[1].Elevation + 5m;

        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsTrue();
        await Assert.That(viewModel.Rows[0].GradeToNext.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task AnInvalidStation_ShowsTheProblemAndBlocksApply()
    {
        var (viewModel, _, _) = NewViewModel();
        viewModel.NewStation = 50m;
        viewModel.AddStationCommand.Execute(null);
        viewModel.NewStation = 100m;
        viewModel.AddStationCommand.Execute(null);

        viewModel.Rows[1].Station = 120m; // now after the next station

        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
        await Assert.That(viewModel.ProfileStatus.Contains("order")).IsTrue();
    }

    [Test]
    public async Task NegativeCurveLength_BlocksApply()
    {
        var (viewModel, _, _) = NewViewModel();
        viewModel.NewStation = 50m;
        viewModel.AddStationCommand.Execute(null);

        viewModel.Rows[1].CurveLength = -5m;

        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task DeleteStation_RemovesAnInteriorRowButNotAnEnd()
    {
        var (viewModel, _, _) = NewViewModel();
        viewModel.NewStation = 50m;
        viewModel.AddStationCommand.Execute(null);

        viewModel.SelectedRow = viewModel.Rows[0];
        viewModel.DeleteStationCommand.Execute(null);
        await Assert.That(viewModel.Rows.Count).IsEqualTo(3);

        viewModel.SelectedRow = viewModel.Rows[1];
        viewModel.DeleteStationCommand.Execute(null);
        await Assert.That(viewModel.Rows.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ResetStations_RestoresTheOriginalProfile()
    {
        var (viewModel, _, _) = NewViewModel();
        viewModel.NewStation = 50m;
        viewModel.AddStationCommand.Execute(null);
        viewModel.Rows[2].Elevation = 99m;

        viewModel.ResetStationsCommand.Execute(null);

        await Assert.That(viewModel.Rows.Count).IsEqualTo(2);
        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
    }

    [Test]
    public async Task Apply_BuildsARequestWithTheEditedProfile()
    {
        var (viewModel, _, alignment) = NewViewModel();
        var closed = false;
        viewModel.ClosingRequest += (s, e) => closed = true;
        viewModel.NewStation = 50m;
        viewModel.AddStationCommand.Execute(null);
        viewModel.Rows[1].Elevation = 12m;
        viewModel.Rows[2].Elevation = 20m;

        viewModel.ApplyCommand.Execute(null);

        await Assert.That(closed).IsTrue();
        await Assert.That(viewModel.Request).IsNotNull();
        await Assert.That(viewModel.Request.Alignment.Id).IsEqualTo(alignment.Id);
        await Assert.That(viewModel.Request.StationsAdded).IsEqualTo(1);
        await Assert.That(viewModel.Request.StationsRemoved).IsEqualTo(0);
        await Assert.That(viewModel.Request.StationsChanged).IsEqualTo(1);

        var profile = AlignmentJsonDocument.Parse(viewModel.Request.Json).ReadProfile();
        await Assert.That(profile.Count).IsEqualTo(3);
        await Assert.That(profile[1].Distance).IsEqualTo(50.0).Within(1e-6);
        await Assert.That(profile[1].Elevation).IsEqualTo(12.0).Within(1e-6);
        await Assert.That(profile[2].Elevation).IsEqualTo(20.0).Within(1e-6);
    }

    [Test]
    public async Task ChangingUnits_KeepsTheSamePhysicalValues()
    {
        var (viewModel, _, _) = NewViewModel();
        viewModel.Rows[1].Elevation = 10m;

        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Meters);

        await Assert.That(viewModel.Rows[1].Station).IsEqualTo(decimal.Round(150m * 0.3048m, 3));
        await Assert.That(viewModel.Rows[1].Elevation).IsEqualTo(decimal.Round(10m * 0.3048m, 3));
    }

    [Test]
    public async Task ChangingUnits_DoesNotMakeUntouchedStationsCountAsEdited()
    {
        var (viewModel, _, _) = NewViewModel();

        viewModel.SelectedDisplayUnit = viewModel.DisplayUnits.First(u => u.DisplayUnit == UnitTypeId.Meters);

        await Assert.That(viewModel.ApplyCommand.CanExecute(null)).IsFalse();
    }
}

public class EditProfileViewTests : TopoAlignUiTest
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

    [Test]
    public async Task View_CanBeCreatedAndBoundToTheViewModel()
    {
        App.RevitDocument = NewDocument();
        var viewModel = new EditProfileViewModel();

        var view = new EditProfileView(viewModel);

        await Assert.That(view.DataContext).IsEqualTo(viewModel);
        await Assert.That(view.Title.StartsWith("Edit Profile ")).IsTrue();
    }
}
