using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandCreateAlignment : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandCreateAlignment>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandCreateAlignment));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        if (!Alignments.AlignmentApi.IsAvailable)
        {
            TaskDialog.Show("Create Alignment", "The Autodesk Civil Alignments add-in is not available in this Revit installation.");
            return Result.Cancelled;
        }

        var viewModel = new CreateAlignmentViewModel();
        var view = new CreateAlignmentView(viewModel);

        return view.ShowDialog() == true ? Result.Succeeded : Result.Cancelled;
    }
}
