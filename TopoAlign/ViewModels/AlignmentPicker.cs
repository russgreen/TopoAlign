using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Serilog;
using TopoAlign.PickFilters;

namespace TopoAlign.ViewModels;

/// <summary>The "click an alignment while the dialog is hidden" step shared by the alignment dialogs.</summary>
internal static class AlignmentPicker
{
    /// <summary>
    /// Asks the user to click an alignment and hands it to <paramref name="select"/>. Cancelling is not an error; any other
    /// failure is logged and shown with <paramref name="dialogTitle"/>.
    /// </summary>
    public static void PickAndSelect(string prompt, string dialogTitle, Action<Element> select, ILogger log)
    {
        try
        {
            var uidoc = App.CachedUiApp.ActiveUIDocument;
            var reference = uidoc.Selection.PickObject(ObjectType.Element, new AlignmentPickFilter(), prompt);
            select(App.RevitDocument.GetElement(reference));
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // user cancelled selection
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed while selecting an alignment");
            TaskDialog.Show(dialogTitle, ex.Message);
        }
    }
}
