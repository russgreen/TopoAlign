#if REVIT2024_OR_GREATER
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Grading;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Grading.Kerb;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandDroppedKerbRamp : IExternalCommand
{
    private const string Title = "Dropped Kerb Ramp";

    private static readonly ILogger log = Log.ForContext<CommandDroppedKerbRamp>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandDroppedKerbRamp));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        var viewModel = new KerbRampViewModel();
        var view = new KerbRampView(viewModel);

        // The dialog hides itself to let the user pick; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        bool pickRequested;
        do
        {
            viewModel.PickRequested = KerbPickKind.None;
            dialogResult = view.ShowDialog();

            pickRequested = viewModel.PickRequested != KerbPickKind.None;
            if (pickRequested)
            {
                viewModel.RunPick();
            }
        }
        while (pickRequested);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Edit after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            var result = KerbRampRevit.Apply(App.RevitDocument, request);

            var summary = $"Created a dropped kerb with a ramp {FormatLength(result.RampLength)} long and flares " +
                          $"{FormatLength(result.StartFlareLength)} and {FormatLength(result.EndFlareLength)} long.{Environment.NewLine}" +
                          $"Removed {result.PointsRemoved} existing toposolid points and added {result.PointsAdded}.";
            foreach (var warning in result.Warnings)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{warning}";
            }

            TaskDialog.Show(Title, summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Dropped kerb ramp failed");
            TaskDialog.Show(Title, ex.Message);
            return Result.Failed;
        }
    }

    /// <summary>A length in feet, shown in the document's length unit.</summary>
    private static string FormatLength(double feet)
    {
        var unit = App.RevitDocument.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
        return $"{UnitUtils.ConvertFromInternalUnits(feet, unit):0.##} {LabelUtils.GetLabelForUnit(unit)}";
    }
}
#endif
