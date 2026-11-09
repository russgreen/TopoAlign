using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>What the sweep makes: one profile, or an assembly of profiles.</summary>
internal enum SweepMode
{
    Profile,
    Assembly
}
