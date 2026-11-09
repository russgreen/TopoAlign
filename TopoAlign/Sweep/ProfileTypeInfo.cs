using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;

namespace TopoAlign.Sweep;

/// <summary>A profile family type loaded in the project that can be used as a road section.</summary>
internal sealed class ProfileTypeInfo
{
    public ProfileTypeInfo(ElementId symbolId, string name)
    {
        SymbolId = symbolId;
        Name = name;
    }

    public ElementId SymbolId { get; }

    /// <summary>"Family : Type", as shown in Revit's type selectors.</summary>
    public string Name { get; }
}
