using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Serilog;
using System;
using System.IO;
using System.Linq;
using TopoAlign.Alignments;

namespace TopoAlign.Commands;

/// <summary>
/// Alignment tool. First slice: read an existing native alignment and dump its infra JSON
/// (used to learn the schema before create/edit is built).
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandAlignmentTool : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandAlignmentTool>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandAlignmentTool));

        var uidoc = commandData.Application.ActiveUIDocument;
        var doc = uidoc.Document;

        if (!AlignmentApi.IsAvailable)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Alignment Tool", "The Autodesk Civil Alignments add-in could not be found, so alignments are not available in this Revit installation.");
            return Result.Cancelled;
        }

        Element element = uidoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .FirstOrDefault(AlignmentApi.IsValid);

        if (element is null)
        {
            if (AlignmentApi.GetAlignmentElements(doc).Count == 0)
            {
                // Diagnostics: valid alignments were not found, so report what is actually there.
                var report = new System.Text.StringBuilder();
                var inCategory = AlignmentApi.GetAlignmentCategoryElements(doc);
                report.AppendLine($"Elements in Alignments category: {inCategory.Count}");

                var candidates = inCategory
                    .Concat(new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).ToElements())
                    .GroupBy(e => e.Id)
                    .Select(g => g.First());

                foreach (var candidate in candidates)
                {
                    var candidateJson = AlignmentApi.GetJson(candidate);
                    if (string.IsNullOrEmpty(candidateJson))
                    {
                        continue;
                    }

                    var path = Path.Combine(Path.GetTempPath(), $"TopoAlign_Alignment_{candidate.Id}.json");
                    File.WriteAllText(path, candidateJson);
                    report.AppendLine($"{candidate.GetType().Name} {candidate.Id} '{candidate.Name}' category '{candidate.Category?.Name}' valid={AlignmentApi.IsValid(candidate)}");
                    report.AppendLine($"  JSON: {path}");
                    log.Information("Alignment JSON candidate {id} -> {path}", candidate.Id, path);
                }

                Autodesk.Revit.UI.TaskDialog.Show("Alignment Tool", "No valid alignments found.\n\n" + report);
                return Result.Cancelled;
            }

            try
            {
                var reference = uidoc.Selection.PickObject(ObjectType.Element, new AlignmentPickFilter(), "Select an alignment");
                element = doc.GetElement(reference);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
        }

        var info = AlignmentApi.GetInfo(element);
        var json = AlignmentApi.GetJson(element);

        string jsonPath = null;
        if (!string.IsNullOrEmpty(json))
        {
            jsonPath = Path.Combine(Path.GetTempPath(), $"TopoAlign_Alignment_{element.Id}.json");
            File.WriteAllText(jsonPath, json);
        }

        Autodesk.Revit.UI.TaskDialog.Show("Alignment Tool",
            $"Name: {info?.Name}\n" +
            $"Start station: {info?.StartStation}\n" +
            $"End station: {info?.EndStation}\n" +
            $"JSON: {jsonPath ?? "not available"}");

        return Result.Succeeded;
    }
}
