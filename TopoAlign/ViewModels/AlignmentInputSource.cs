using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using TopoAlign.Alignments;
using TopoAlign.Preview;

namespace TopoAlign.ViewModels;

/// <summary>Where the alignment's plan geometry comes from.</summary>
internal enum AlignmentInputSource
{
    /// <summary>Model or detail lines and arcs picked in the model.</summary>
    Lines,

    /// <summary>Points of intersection picked in plan, with a radius and transition spirals at each bend.</summary>
    Points,

    /// <summary>Lines, arcs and polylines picked on an imported or linked CAD drawing.</summary>
    Cad
}
