using Autodesk.Revit.DB;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TopoAlign.Alignments.Civil;

internal class AlignmentInfo
{
    public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double DisplayedStartStation { get; set; }
    public double DisplayedEndStation { get; set; }
}
