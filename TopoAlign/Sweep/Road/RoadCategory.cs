using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep.Road;

/// <summary>A Revit category a road can be filed under, with the name shown to the user.</summary>
internal sealed class RoadCategory
{
    public RoadCategory(BuiltInCategory category, string name)
    {
        Category = category;
        Name = name;
    }

    public BuiltInCategory Category { get; }
    public string Name { get; }
}
