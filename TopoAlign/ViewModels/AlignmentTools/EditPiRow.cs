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

namespace TopoAlign.ViewModels.AlignmentTools;

/// <summary>
/// One point of intersection of the alignment being edited. The exact values are in feet (internal units); the display values
/// are what the grid shows and edits, in the dialog's units.
/// </summary>
internal sealed partial class EditPiRow : ObservableObject
{
    public EditPiRow(double xFeet, double yFeet, double radiusFeet, double spiralFeet)
    {
        XFeet = xFeet;
        YFeet = yFeet;
        RadiusFeet = radiusFeet;
        SpiralFeet = spiralFeet;
    }

    public double XFeet { get; set; }
    public double YFeet { get; set; }
    public double RadiusFeet { get; set; }
    public double SpiralFeet { get; set; }

    [ObservableProperty]
    private int _number;

    /// <summary>The first and last points are the ends of the alignment and have no curve.</summary>
    [ObservableProperty]
    private bool _isEnd;

    [ObservableProperty]
    private decimal _x;

    [ObservableProperty]
    private decimal _y;

    [ObservableProperty]
    private decimal _radius;

    [ObservableProperty]
    private decimal _spiralLength;
}
