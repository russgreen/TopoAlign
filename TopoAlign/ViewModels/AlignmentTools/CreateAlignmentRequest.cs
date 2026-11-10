using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Preview;

namespace TopoAlign.ViewModels.AlignmentTools;

internal sealed class CreateAlignmentRequest
{
    public CreateAlignmentRequest(string name, string json, double planLength, IList<ElementId>? sourceCurveIdsToDelete, string notice = "")
    {
        Name = name;
        Json = json;
        PlanLength = planLength;
        SourceCurveIdsToDelete = sourceCurveIdsToDelete;
        Notice = notice;
    }

    /// <summary>Extra text for the summary, for example curves that could not take spirals.</summary>
    public string Notice { get; }

    public string Name { get; }
    public string Json { get; }

    /// <summary>Alignment length in feet (internal units).</summary>
    public double PlanLength { get; }
    public IList<ElementId>? SourceCurveIdsToDelete { get; }
}
