using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>A way of entering a slope, with the name the editor shows for it.</summary>
internal sealed class SlopeModeOption
{
    public SlopeModeOption(TopoAlign.Grading.Slope.SlopeMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public TopoAlign.Grading.Slope.SlopeMode Mode { get; }
    public string Label { get; }
}
