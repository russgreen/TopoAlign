using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows;
using TopoAlign.Alignments.Civil;

namespace TopoAlign.Commands.Shared;

/// <summary>The steps the dialog-driven commands have in common.</summary>
internal static class CommandFlow
{
    /// <summary>Remembers the active Revit application and document for the rest of the command.</summary>
    public static void Initialise(ExternalCommandData commandData)
    {
        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;
    }

    /// <summary>
    /// Checks the installed Civil Alignments add-in has what the command needs. When it doesn't, tells the user why
    /// (using <paramref name="title"/> as the dialog title) and returns false.
    /// </summary>
    public static bool AlignmentFeaturesSupported(AlignmentFeature features, string title)
    {
        if (!AlignmentApi.Supports(features, out var problem))
        {
            TaskDialog.Show(title, problem);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Offers the elements that are already selected to <paramref name="select"/>, in order, until it accepts one.
    /// Elements the filter rejects are skipped.
    /// </summary>
    public static void UseSelectedElement(UIDocument uidoc, ISelectionFilter filter, Func<Element, bool> select)
    {
        foreach (var id in uidoc.Selection.GetElementIds())
        {
            var element = App.RevitDocument.GetElement(id);
            if (filter.AllowElement(element) && select(element))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Shows a dialog that hides itself to let the user pick something. Each time it asks for a pick, <paramref name="runPick"/>
    /// runs here in the API context and the same dialog is shown again. Returns the result of the last time it was shown.
    /// </summary>
    public static bool? ShowDialogWithPicks(Window view, Action resetPick, Func<bool> pickRequested, Action runPick)
    {
        bool? dialogResult;
        do
        {
            resetPick();
            dialogResult = view.ShowDialog();

            if (pickRequested())
            {
                runPick();
            }
        }
        while (pickRequested());

        return dialogResult;
    }
}
