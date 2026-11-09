using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>What the dialog was hidden to let the user pick.</summary>
internal enum KerbSweepPickKind
{
    None,
    Edges,
    Lines
}
