using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;

namespace TopoAlign.Tests;

public class PickFilterTests : TopoAlignUiTest
{
    private static HermiteSpline Spline()
    {
        return HermiteSpline.Create(new List<XYZ> { new(0, 0, 0), new(5, 5, 0), new(10, 0, 0) }, false);
    }

    // ---- CurvePickFilter ----------------------------------------------------------------------------------------

    [Test]
    public async Task CurvePickFilter_AcceptsModelLinesAndArcs()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var arc = NewModelCurve(document, QuarterArcCcw(10));
        var filter = new CurvePickFilter();

        await Assert.That(filter.AllowElement(line)).IsTrue();
        await Assert.That(filter.AllowElement(arc)).IsTrue();
    }

    [Test]
    public async Task CurvePickFilter_RejectsSplinesAndOtherElements()
    {
        var document = NewDocument();
        var spline = NewModelCurve(document, Spline());
        var filter = new CurvePickFilter();

        await Assert.That(filter.AllowElement(spline)).IsFalse();
        await Assert.That(filter.AllowElement(document.ActiveProjectLocation)).IsFalse();
        await Assert.That(filter.AllowReference(null, XYZ.Zero)).IsFalse();
    }

    // ---- LinePickFilter -----------------------------------------------------------------------------------------

    [Test]
    public async Task LinePickFilter_AcceptsAnyModelCurve()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var spline = NewModelCurve(document, Spline());
        var filter = new LinePickFilter();

        await Assert.That(filter.AllowElement(line)).IsTrue();
        await Assert.That(filter.AllowElement(spline)).IsTrue();
    }

    [Test]
    public async Task LinePickFilter_RejectsOtherElements()
    {
        var document = NewDocument();
        var filter = new LinePickFilter();

        await Assert.That(filter.AllowElement(document.ActiveProjectLocation)).IsFalse();
        await Assert.That(filter.AllowReference(null, XYZ.Zero)).IsFalse();
    }

    // ---- category filters ---------------------------------------------------------------------------------------

    [Test]
    public async Task TopoPickFilter_RejectsModelLinesAndElementsWithoutACategory()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var filter = new TopoPickFilter();

        await Assert.That(filter.AllowElement(line)).IsFalse();
        await Assert.That(filter.AllowElement(document.ActiveProjectLocation)).IsFalse();
        await Assert.That(filter.AllowReference(null, XYZ.Zero)).IsFalse();
    }

    [Test]
    public async Task FloorPickFilter_RejectsModelLinesAndElementsWithoutACategory()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var filter = new FloorPickFilter();

        await Assert.That(filter.AllowElement(line)).IsFalse();
        await Assert.That(filter.AllowElement(document.ActiveProjectLocation)).IsFalse();
        await Assert.That(filter.AllowReference(null, XYZ.Zero)).IsFalse();
    }

    [Test]
    public async Task ElemPickFilter_RejectsModelLinesAndElementsWithoutACategory()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var filter = new ElemPickFilter();

        await Assert.That(filter.AllowElement(line)).IsFalse();
        await Assert.That(filter.AllowElement(document.ActiveProjectLocation)).IsFalse();
        await Assert.That(filter.AllowReference(null, XYZ.Zero)).IsFalse();
    }

    // ---- FacePickFilter -----------------------------------------------------------------------------------------

    [Test]
    public async Task FacePickFilter_AllowsAnyElement()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));

        await Assert.That(new FacePickFilter(document).AllowElement(line)).IsTrue();
    }

    [Test]
    public async Task FacePickFilter_RejectsReferencesThatAreNotFaces()
    {
        var document = NewDocument();
        var line = NewModelCurve(document, Ln(0, 0, 10, 0));
        var reference = new Reference(line);

        await Assert.That(new FacePickFilter(document).AllowReference(reference, XYZ.Zero)).IsFalse();
    }

    // ---- FailureHandler -----------------------------------------------------------------------------------------

    [Test]
    public async Task FailureHandler_StartsWithNoErrorInformation()
    {
        var handler = new FailureHandler();

        await Assert.That(handler.ErrorMessage).IsEqualTo(string.Empty);
        await Assert.That(handler.ErrorSeverity).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task FailureHandler_CleanTransaction_LeavesNoErrorInformation()
    {
        var document = NewDocument();
        var handler = new FailureHandler();

        using (var transaction = new Transaction(document, "Clean"))
        {
            transaction.Start();
            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(handler);
            transaction.SetFailureHandlingOptions(options);

            var plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
            document.Create.NewModelCurve(Ln(0, 0, 10, 0), SketchPlane.Create(document, plane));

            await Assert.That(transaction.Commit()).IsEqualTo(TransactionStatus.Committed);
        }

        await Assert.That(handler.ErrorMessage).IsEqualTo(string.Empty);
    }
}
