using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels.RoadTools;

/// <summary>One side's topo grading as the user types it: slopes in the chosen slope mode, the widest run in display units.</summary>
internal sealed partial class SideGradingRow : ObservableObject
{
    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private decimal _cutValue = 2m;

    [ObservableProperty]
    private decimal _fillValue = 3m;

    [ObservableProperty]
    private decimal _maxWidth;
}
