using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using TopoAlign.Sweep;
using TopoAlign.ViewModels;
using TopoAlign.Views;
using TopoAlign.Sweep.Kerb;
using TopoAlign.Sweep.Road;

namespace TopoAlign.Commands;

/// <summary>Sweeps a profile along picked edges or model lines, keeping the profile vertical, to make a kerb.</summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandKerb : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandKerb>();

    private const string Title = "Kerb";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandKerb));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        var viewModel = new KerbViewModel();

        // model lines that are already selected are used without asking
        var uidoc = commandData.Application.ActiveUIDocument;
        viewModel.AddModelCurves(uidoc.Selection.GetElementIds().Select(id => App.RevitDocument.GetElement(id)).Where(e => e is not null));

        var view = new KerbView(viewModel);

        // The dialog hides itself to let the user pick; the pick runs here in the API context,
        // then the same dialog is shown again.
        bool? dialogResult;
        do
        {
            viewModel.PickRequested = KerbSweepPickKind.None;
            dialogResult = view.ShowDialog();

            if (viewModel.PickRequested != KerbSweepPickKind.None)
            {
                viewModel.RunPick(viewModel.PickRequested);
            }
        }
        while (viewModel.PickRequested != KerbSweepPickKind.None);

        if (dialogResult != true || viewModel.Request is not { } request)
        {
            return Result.Cancelled;
        }

        // Build after the dialog has closed so the transaction runs in the Revit API context.
        try
        {
            // which side the profile is on, as the user sees it; each flip at review swaps it
            var profileOnLeft = request.Flipped;
            if (request.Review)
            {
                request.Options.Review = kerb =>
                {
                    var choice = Review(uidoc, kerb, profileOnLeft);
                    if (choice == KerbReviewChoice.Flip)
                    {
                        profileOnLeft = !profileOnLeft;
                    }

                    return choice;
                };
            }

            var result = KerbBuilder.Create(App.RevitDocument, request.Chains, request.Profile, request.Options);
            if (result.Cancelled)
            {
                return Result.Cancelled;
            }

            if (result.Flipped)
            {
                // remember the side the user ended up choosing
                var settings = new Services.Settings();
                settings.LoadSettings();
                settings.KerbSweepFlip = request.Flipped != result.Flipped;
                settings.SaveSettings();
            }

            var summary = $"Kerb created from {result.RunCount} run{(result.RunCount == 1 ? string.Empty : "s")} and {result.SectionCount} sections" +
                          (request.Options.Output == RoadOutput.Family ? ", as a family." : ".");

            if (!string.IsNullOrEmpty(result.Notice))
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.Notice}";
            }

            if (result.SkippedSpans > 0)
            {
                summary += $"{Environment.NewLine}{Environment.NewLine}{result.SkippedSpans} span{(result.SkippedSpans == 1 ? string.Empty : "s")} could not be built, " +
                           "so the kerb has gaps there. This usually means a corner or curve is tighter than the profile is wide.";
            }

            TaskDialog.Show(Title, summary);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Kerb failed");
            TaskDialog.Show(Title, ex.Message);
            return Result.Failed;
        }
    }

    /// <summary>Shows the trial kerb in the model, selected, and asks whether to keep it, flip it or cancel.</summary>
    private static KerbReviewChoice Review(UIDocument uidoc, KerbResult kerb, bool profileOnLeft)
    {
        try
        {
            uidoc.Selection.SetElementIds(new List<ElementId> { kerb.KerbId });
            uidoc.RefreshActiveView();
        }
        catch (Exception ex)
        {
            log.Information(ex, "Could not select the trial kerb");
        }

        var dialog = new TaskDialog(Title)
        {
            MainInstruction = "Is the kerb on the right side?",
            MainContent = "The kerb is shown selected in the model, but not kept yet. Its profile is on the " +
                          (profileOnLeft ? "left" : "right") +
                          " of the direction of the first edge picked (a closed loop runs anticlockwise, so the right is outside).",
            CommonButtons = TaskDialogCommonButtons.Cancel
        };

        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Keep the kerb", "Leave it where it is.");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Flip to the other side", "Build it again with the profile on the other side of the edges, then ask again.");

        // the default button can only be set once the command links exist
        dialog.DefaultButton = TaskDialogResult.CommandLink1;

        var choice = dialog.Show() switch
        {
            TaskDialogResult.CommandLink1 => KerbReviewChoice.Keep,
            TaskDialogResult.CommandLink2 => KerbReviewChoice.Flip,
            _ => KerbReviewChoice.Cancel
        };

        if (choice != KerbReviewChoice.Keep)
        {
            try
            {
                // the trial kerb is about to be rolled back
                uidoc.Selection.SetElementIds(new List<ElementId>());
            }
            catch (Exception ex)
            {
                log.Information(ex, "Could not clear the selection");
            }
        }

        return choice;
    }
}
