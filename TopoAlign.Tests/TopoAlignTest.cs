using Autodesk.Revit.DB;
using Nice3point.TUnit.Revit;

namespace TopoAlign.Tests;

/// <summary>
/// Base class for tests that run in Revit's API context and only use RevitAPI types.
/// Tests that touch RevitAPIUI types (App, selection filters, TaskDialog) use <see cref="TopoAlignUiTest"/> instead.
/// </summary>
public abstract class TopoAlignTest : RevitApiTest
{
    private readonly List<Document> _documents = new();

    [After(Test)]
    public void CloseDocuments()
    {
        DocumentCleanup.Close(_documents);
    }

    /// <summary>Creates a new, empty project document that is closed after the test.</summary>
    protected Document NewDocument(UnitSystem unitSystem = UnitSystem.Imperial)
    {
        var document = Application.NewProjectDocument(unitSystem);
        _documents.Add(document);
        return document;
    }
}

/// <summary>
/// Base class for tests that need RevitAPIUI types. These run through RevitUnit's UI executor.
/// </summary>
public abstract class TopoAlignUiTest : RevitApiUiTest
{
    private readonly List<Document> _documents = new();

    [After(Test)]
    public void CloseDocuments()
    {
        DocumentCleanup.Close(_documents);
    }

    /// <summary>Creates a new, empty project document that is closed after the test.</summary>
    protected Document NewDocument(UnitSystem unitSystem = UnitSystem.Imperial)
    {
        var document = UiApplication.Application.NewProjectDocument(unitSystem);
        _documents.Add(document);
        return document;
    }
}

internal static class DocumentCleanup
{
    public static void Close(List<Document> documents)
    {
        foreach (var document in documents)
        {
            try
            {
                if (document.IsValidObject)
                {
                    document.Close(false);
                }
            }
            catch
            {
                // best effort clean-up
            }
        }

        documents.Clear();
    }
}
