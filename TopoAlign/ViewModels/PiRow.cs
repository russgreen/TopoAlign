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

/// <summary>One picked point of intersection in the point list. Radius and spiral length are in display units.</summary>
internal sealed partial class PiRow : ObservableObject
{
    public PiRow(double x, double y, double z, decimal radius, decimal spiralLength)
    {
        X = x;
        Y = y;
        Z = z;
        _radius = radius;
        _spiralLength = spiralLength;
    }

    /// <summary>Position in feet (internal units).</summary>
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private decimal _radius;

    [ObservableProperty]
    private decimal _spiralLength;
}
