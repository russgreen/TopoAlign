using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using TopoAlign.Alignments;

namespace TopoAlign.Tests;

/// <summary>
/// Integration tests against Autodesk's Civil Alignments add-in, which ships with Revit.
/// </summary>
public class AlignmentApiTests : TopoAlignTest
{
    // ---- availability and queries on an empty model ---------------------------------------------------------------

    [Test]
    public async Task IsAvailable_WhenRevitShipsCivilAlignments_IsTrue()
    {
        await Assert.That(AlignmentApi.IsAvailable).IsTrue();
    }

    [Test]
    public async Task IsValid_Null_IsFalse()
    {
        await Assert.That(AlignmentApi.IsValid(null)).IsFalse();
    }

    [Test]
    public async Task IsValid_OrdinaryElement_IsFalse()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(AlignmentApi.IsValid(line)).IsFalse();
    }

    [Test]
    public async Task GetAlignmentElements_EmptyDocument_ReturnsNothing()
    {
        var document = NewDocument();

        await Assert.That(AlignmentApi.GetAlignmentElements(document).Count).IsEqualTo(0);
        await Assert.That(AlignmentApi.GetAlignmentCategoryElements(document).Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetInfo_Null_ReturnsNull()
    {
        await Assert.That(AlignmentApi.GetInfo(null)).IsNull();
    }

    [Test]
    public async Task GetInfo_NonAlignmentElement_ReturnsNull()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(AlignmentApi.GetInfo(line)).IsNull();
    }

    [Test]
    public async Task GetPointAtStation_NonAlignmentElement_ReturnsNull()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(AlignmentApi.GetPointAtStation(line, 0)).IsNull();
        await Assert.That(AlignmentApi.GetPointAtStation(null, 0)).IsNull();
    }

    [Test]
    public async Task GetJson_NullElement_ReturnsNull()
    {
        await Assert.That(AlignmentApi.GetJson(null)).IsNull();
    }

    // ---- Create argument checks ---------------------------------------------------------------------------------

    [Test]
    public async Task Create_NullDocument_Throws()
    {
        var exception = Catch(() => AlignmentApi.Create(null, "A", "{}", 0));

        await Assert.That(exception is ArgumentNullException).IsTrue();
    }

    [Test]
    public async Task Create_EmptyJson_Throws()
    {
        var document = NewDocument();

        var blank = Catch(() => AlignmentApi.Create(document, "A", "  ", 0));
        var none = Catch(() => AlignmentApi.Create(document, "A", null, 0));

        await Assert.That(blank is ArgumentException).IsTrue();
        await Assert.That(none is ArgumentException).IsTrue();
    }

    [Test]
    public async Task Create_InvalidJson_ThrowsAndLeavesNoAlignment()
    {
        var document = NewDocument();

        var exception = Catch(() => AlignmentApi.Create(document, "A", "{}", 0));

        await Assert.That(exception).IsNotNull();
        await Assert.That(AlignmentApi.GetAlignmentCategoryElements(document).Count).IsEqualTo(0);
    }

    // ---- Create round trip --------------------------------------------------------------------------------------

    [Test]
    public async Task Create_FromBuilderJson_CreatesAValidAlignment()
    {
        var document = NewDocument();

        var id = CreateAlignment(document);

        await Assert.That(id).IsNotEqualTo(ElementId.InvalidElementId);
        var element = document.GetElement(id);
        await Assert.That(element).IsNotNull();
        await Assert.That(AlignmentApi.IsValid(element)).IsTrue();
        await Assert.That(AlignmentApi.GetAlignmentElements(document).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_SetsAlignmentName()
    {
        var document = NewDocument();

        var id = CreateAlignment(document, "Access Road");

        var info = AlignmentApi.GetInfo(document.GetElement(id));
        await Assert.That(info).IsNotNull();
        await Assert.That(info.Name).IsEqualTo("Access Road");
        await Assert.That(info.ElementId).IsEqualTo(id);
    }

    [Test]
    public async Task Create_AlignmentHasPositiveLength()
    {
        var document = NewDocument();

        var id = CreateAlignment(document);

        var info = AlignmentApi.GetInfo(document.GetElement(id));
        await Assert.That(info.EndStation).IsGreaterThan(info.StartStation);
    }

    [Test]
    public async Task Create_StartStation_ShiftsTheStationsButNotTheLength()
    {
        var document = NewDocument();

        var fromZero = AlignmentApi.GetInfo(document.GetElement(CreateAlignment(document, "Zero", 0.0)));
        var shifted = AlignmentApi.GetInfo(document.GetElement(CreateAlignment(document, "Shifted", 500.0)));

        await Assert.That(shifted.StartStation).IsGreaterThan(fromZero.StartStation);
        await Assert.That(shifted.EndStation - shifted.StartStation).IsEqualTo(fromZero.EndStation - fromZero.StartStation).Within(1e-6);
    }

    [Test]
    public async Task Create_PointAtStart_IsTheFirstPointOfTheFirstCurve()
    {
        var document = NewDocument();

        var id = CreateAlignment(document);

        var info = AlignmentApi.GetInfo(document.GetElement(id));
        var point = AlignmentApi.GetPointAtStation(document.GetElement(id), info.StartStation);
        await Assert.That(point).IsNotNull();
        await Assert.That(point.X).IsEqualTo(0.0).Within(1e-3);
        await Assert.That(point.Y).IsEqualTo(0.0).Within(1e-3);
    }

    [Test]
    public async Task Create_StoresJsonThatCanBeReadBack()
    {
        var document = NewDocument();

        var id = CreateAlignment(document, "Json Road");

        var json = AlignmentApi.GetJson(document.GetElement(id));
        await Assert.That(json).IsNotNull();
        await Assert.That(json.Contains("Json Road")).IsTrue();
    }

    [Test]
    public async Task Create_CanCreateMoreThanOneAlignment()
    {
        var document = NewDocument();

        CreateAlignment(document, "One");
        CreateAlignment(document, "Two");

        var names = AlignmentApi.GetAlignmentElements(document)
            .Select(AlignmentApi.GetInfo)
            .Select(info => info.Name)
            .OrderBy(name => name)
            .ToList();
        await Assert.That(names.Count).IsEqualTo(2);
        await Assert.That(names[0]).IsEqualTo("One");
        await Assert.That(names[1]).IsEqualTo("Two");
    }

    [Test]
    public async Task Create_WithSourceCurves_DeletesThemAfterCreation()
    {
        var document = NewDocument();
        var firstId = NewModelCurve(document, Ln(0, 0, 100, 0)).Id;
        var secondId = NewModelCurve(document, Ln(100, 0, 100, 50)).Id;

        CreateAlignment(document, delete: new List<ElementId> { firstId, secondId });

        // read the ids first: a deleted element can no longer be asked for its Id
        await Assert.That(document.GetElement(firstId)).IsNull();
        await Assert.That(document.GetElement(secondId)).IsNull();
    }

    [Test]
    public async Task Create_WithoutSourceCurves_LeavesModelLinesAlone()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 100, 0));

        CreateAlignment(document);

        await Assert.That(document.GetElement(line.Id)).IsNotNull();
    }
}

/// <summary>The selection filter implements a RevitAPIUI interface, so it is tested through the UI executor.</summary>
public class AlignmentPickFilterTests : TopoAlignUiTest
{
    [Test]
    public async Task AlignmentPickFilter_AcceptsCreatedAlignmentAndRejectsOtherElements()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var id = CreateAlignment(document);
        var filter = new AlignmentPickFilter();

        await Assert.That(filter.AllowElement(document.GetElement(id))).IsTrue();
        await Assert.That(filter.AllowElement(line)).IsFalse();
        await Assert.That(filter.AllowReference(null, XYZ.Zero)).IsFalse();
    }
}
