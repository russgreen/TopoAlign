using Autodesk.Revit.DB;

namespace TopoAlign.Models;
internal class DisplayUnitModel
{
#if REVIT2018 || REVIT2019 || REVIT2020
    public DisplayUnitType DisplayUnit { get; set; }
#else
    public ForgeTypeId DisplayUnit { get; set; }
#endif
    public string Label { get; set; }
}
