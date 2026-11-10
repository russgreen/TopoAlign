using Autodesk.Revit.DB;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.ViewModels.AlignmentTools;

/// <summary>The plan geometry for the current input: Revit curves where they can be used as picked, otherwise plan segments.</summary>
internal sealed class PlanLayout
{
    public PlanLayout(List<Curve>? curves, List<PlanSegment> segments, double flatElevation, string notice = "")
    {
        Curves = curves;
        Segments = segments;
        FlatElevation = flatElevation;
        Notice = notice;
    }

    /// <summary>Anything the user should be told about, such as curves that could not take spirals; empty when there is nothing.</summary>
    public string Notice { get; }

    /// <summary>The picked curves (reversed if asked), or null when spirals were added or the points were laid out.</summary>
    public List<Curve>? Curves { get; }

    public List<PlanSegment> Segments { get; }

    /// <summary>Internal elevation of the flat profile used when the profile isn't draped.</summary>
    public double FlatElevation { get; }
}
