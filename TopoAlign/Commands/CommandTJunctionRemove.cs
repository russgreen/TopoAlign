using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Extensions;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Civil;
using TopoAlign.Commands.Shared;
using TopoAlign.Sweep.Road;

namespace TopoAlign.Commands;

/// <summary>
/// Removes a T-junction made by <see cref="CommandTJunction"/>: its road surface, the voids it put in the road part families (which
/// puts the cut road parts back) and its kerb return alignments. The side road alignment stays as it was trimmed, and road parts that are plain
/// direct shapes keep the geometry they were cut to.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandTJunctionRemove : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandTJunctionRemove>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandTJunctionRemove));

        CommandFlow.Initialise(commandData);

        var uidoc = commandData.Application.ActiveUIDocument;
        var document = uidoc.Document;

        try
        {
            var picked = uidoc.Selection.GetElementIds().Select(id => document.GetElement(id)).FirstOrDefault(JunctionPartFilter.IsJunctionPart);
            if (picked is null)
            {
                var reference = uidoc.Selection.PickObject(ObjectType.Element, new JunctionPartFilter(), "Select the junction surface or the side road alignment");
                picked = document.GetElement(reference);
            }

            // the junctions to remove: the picked surface's own, or every one the picked side alignment has
            var keys = new List<string>();
            if (picked is DirectShape pickedShape)
            {
                keys.Add(pickedShape.ApplicationDataId);
            }
            else
            {
                keys.AddRange(document.CollectElements()
                    .OfClass(typeof(DirectShape))
                    .Cast<DirectShape>()
                    .Where(shape => shape.ApplicationId == TJunctionSurfaceBuilder.ApplicationId && TJunctionKey.IsFor(shape.ApplicationDataId, picked.UniqueId))
                    .Select(shape => shape.ApplicationDataId)
                    .Distinct());
                if (keys.Count == 0)
                {
                    keys.Add(picked.UniqueId);
                }
            }

            var sideUniqueId = TJunctionKey.SideUniqueId(keys[0]);
            var side = document.GetElement(sideUniqueId);
            if (side is null)
            {
                TaskDialog.Show("Remove T-Junction", "The side road alignment of that junction is no longer in the model.");
                return Result.Cancelled;
            }

            var sideName = AlignmentApi.GetInfo(side)?.Name ?? "Side road";
            var kerbNames = new HashSet<string>();
            foreach (var key in keys)
            {
                // a key from an earlier version is the side alignment's id alone, and its kerb returns have no tag in their names
                var tag = key.Contains('|') ? $" ({TJunctionKey.Short(key)})" : string.Empty;
                kerbNames.Add($"{sideName} kerb L{tag}");
                kerbNames.Add($"{sideName} kerb R{tag}");
            }

            var kerbs = AlignmentApi.GetAlignmentElements(document)
                .Where(e => AlignmentApi.GetInfo(e)?.Name is { } name && kerbNames.Contains(name))
                .ToList();
            var voids = new CutOutcome();

            var confirm = new TaskDialog("Remove T-Junction")
            {
                MainInstruction = keys.Count == 1 ? $"Remove the T-junction of '{sideName}'?" : $"Remove the {keys.Count} T-junctions of '{sideName}'?",
                MainContent = $"This deletes the junction surface, takes the junction voids out of the road part families (the parts they cut are put back) "
                              + $"and deletes {kerbs.Count} kerb return alignment{(kerbs.Count == 1 ? "" : "s")}.{Environment.NewLine}{Environment.NewLine}"
                              + "The side road alignment keeps its trimmed length, and road parts that are direct shapes keep the geometry they were cut to.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No
            };
            if (confirm.Show() != TaskDialogResult.Yes)
            {
                return Result.Cancelled;
            }

            // the families are edited and reloaded with no transaction open
            var families = 0;
            foreach (var key in keys)
            {
                families += JunctionVoids.RemoveVoids(document, key, voids);
            }

            using var transaction = new Transaction(document, "Remove T-junction");
            transaction.Start();

            var surfaces = 0;
            foreach (var key in keys)
            {
                surfaces += TJunctionSurfaceBuilder.DeletePrevious(document, key);
                JunctionVoids.DeleteRecords(document, key);
            }

            if (kerbs.Count > 0)
            {
                document.Delete(kerbs.Select(k => k.Id).ToList());
            }

            transaction.Commit();

            TaskDialog.Show("Remove T-Junction", $"Removed {surfaces} surface, the voids in {families} road part famil{(families == 1 ? "y" : "ies")} and {kerbs.Count} kerb return alignments."
                + (voids.Warnings.Count > 0 ? Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, voids.Warnings.Distinct()) : string.Empty));
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Removing the T-junction failed");
            TaskDialog.Show("Remove T-Junction", ex.Message);
            return Result.Failed;
        }
    }
}
