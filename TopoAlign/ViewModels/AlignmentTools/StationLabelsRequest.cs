using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Globalization;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels.AlignmentTools;

/// <summary>What the Station Labels dialog hands back to the command.</summary>
internal sealed class StationLabelsRequest
{
    public StationLabelsRequest(Element alignment, string alignmentName, View view, ElementId labelTypeId,
        double? startStation, double? endStation, double? interval, double? offset, bool hasLeader)
    {
        Alignment = alignment;
        AlignmentName = alignmentName;
        View = view;
        LabelTypeId = labelTypeId;
        StartStation = startStation;
        EndStation = endStation;
        Interval = interval;
        Offset = offset;
        HasLeader = hasLeader;
    }

    public Element Alignment { get; }
    public string AlignmentName { get; }
    public View View { get; }
    public ElementId LabelTypeId { get; }

    /// <summary>Feet (internal units); null means the alignment's own range.</summary>
    public double? StartStation { get; }
    public double? EndStation { get; }

    /// <summary>Feet (internal units); null means Autodesk's default for the document's stationing units.</summary>
    public double? Interval { get; }

    /// <summary>Paper-space feet, positive to the right of the alignment; null means Autodesk's default.</summary>
    public double? Offset { get; }

    public bool HasLeader { get; }
}
