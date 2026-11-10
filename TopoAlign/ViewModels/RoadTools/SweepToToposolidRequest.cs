#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels.RoadTools;

/// <summary>What the Sweep to Toposolid dialog hands back to the command.</summary>
internal sealed class SweepToToposolidRequest
{
    public SweepToToposolidRequest(Element road, ElementId typeId, ElementId levelId, bool deleteRoad)
    {
        Road = road;
        TypeId = typeId;
        LevelId = levelId;
        DeleteRoad = deleteRoad;
    }

    public Element Road { get; }
    public ElementId TypeId { get; }
    public ElementId LevelId { get; }
    public bool DeleteRoad { get; }
}
#endif
