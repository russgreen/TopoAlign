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
using TopoAlign.Grading.Slope;

namespace TopoAlign.ViewModels.GradingTools;

internal sealed class SlopeModeItem
{
    public SlopeModeItem(SlopeMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public SlopeMode Mode { get; }
    public string Label { get; }
}
#endif
