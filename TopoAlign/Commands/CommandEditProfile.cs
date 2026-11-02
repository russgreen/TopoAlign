using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandEditProfile : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandEditProfile>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandEditProfile));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        if (!AlignmentApi.Supports(AlignmentApi.AlignmentFeature.Read | AlignmentApi.AlignmentFeature.Update, out var problem))
        {
            TaskDialog.Show("Edit Profile", problem);
            return Result.Cancelled;
        }

        var viewModel = new EditProfileViewModel();

        // an alignment that is already selected is used without asking
        var uidoc = commandData.Application.ActiveUIDocument;
        var filter = new AlignmentPickFilter();
        foreach (var id in uidoc.Selection.GetElementIds())
        {
            var element = App.RevitDocument.GetElement(id);
            if (filter.AllowElement(element) && viewModel.SelectAlignment(element))
            {
                break;
            }
        }

        var view = new EditProfileView(viewModel);

        // The dialog hides itself to let the user pick an alignment; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        do
        {
            viewModel.PickRequested = false;
            dialogResult = view.ShowDialog();

            if (viewModel.PickRequested)
            {
                viewModel.PickAlignmentElement();
            }
        }
        while (viewModel.PickRequested);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Update after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            AlignmentApi.Update(App.RevitDocument, request.Alignment, request.Json, request.ProfileLength, null);

            var changes = new List<string>();
            if (request.StationsAdded > 0)
            {
                changes.Add($"{request.StationsAdded} station{(request.StationsAdded == 1 ? string.Empty : "s")} added");
            }

            if (request.StationsRemoved > 0)
            {
                changes.Add($"{request.StationsRemoved} removed");
            }

            if (request.StationsChanged > 0)
            {
                changes.Add($"{request.StationsChanged} changed");
            }

            TaskDialog.Show("Edit Profile", $"Profile of '{request.Name}' updated: {string.Join(", ", changes)}.");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Edit profile failed");
            TaskDialog.Show("Edit Profile", ex.Message);
            return Result.Failed;
        }
    }
}
