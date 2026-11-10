using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels.AlignmentTools;

/// <summary>What the Edit Alignment dialog hands back to the command: the alignment and its edited JSON.</summary>
internal sealed class EditAlignmentRequest
{
    public EditAlignmentRequest(Element alignment, string name, string json, double planLength, bool renamed, bool startStationChanged, bool reversed, bool planChanged = false)
    {
        Alignment = alignment;
        Name = name;
        Json = json;
        PlanLength = planLength;
        Renamed = renamed;
        StartStationChanged = startStationChanged;
        Reversed = reversed;
        PlanChanged = planChanged;
    }

    public Element Alignment { get; }
    public string Name { get; }
    public string Json { get; }

    /// <summary>Alignment length in feet (internal units).</summary>
    public double PlanLength { get; }

    public bool Renamed { get; }
    public bool StartStationChanged { get; }
    public bool Reversed { get; }

    /// <summary>True when the points, radii or spirals of the alignment were edited.</summary>
    public bool PlanChanged { get; }
}
