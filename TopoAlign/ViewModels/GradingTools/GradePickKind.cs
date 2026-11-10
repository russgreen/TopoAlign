using Nice3point.Revit.Extensions;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Grading;

namespace TopoAlign.ViewModels.GradingTools;

/// <summary>What the user is being asked to pick while the dialog is hidden.</summary>
internal enum GradePickKind
{
    None,
    Line,
    Side,
    Toposolid
}
#endif
