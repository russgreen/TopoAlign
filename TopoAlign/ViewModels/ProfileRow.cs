using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Profile;

namespace TopoAlign.ViewModels;

/// <summary>
/// One station of the profile in the grid. Station, elevation and curve length are in display units; a value the user has
/// not touched is written back from the original, so unit rounding never changes it.
/// </summary>
internal sealed partial class ProfileRow : ObservableObject
{
    public ProfileRow(ProfileVertex? source)
    {
        Source = source;
    }

    /// <summary>The station as read from the alignment; null for a station added in the dialog.</summary>
    public ProfileVertex? Source { get; }

    public bool StationEdited { get; set; }
    public bool ElevationEdited { get; set; }
    public bool CurveLengthEdited { get; set; }

    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditStation))]
    [NotifyPropertyChangedFor(nameof(CanEditCurveLength))]
    private bool _isEnd;

    [ObservableProperty]
    private decimal _station;

    [ObservableProperty]
    private decimal _elevation;

    [ObservableProperty]
    private decimal _curveLength;

    /// <summary>Grade from this station to the next, as text such as "2.50 %".</summary>
    [ObservableProperty]
    private string _gradeToNext = string.Empty;

    /// <summary>Curve length actually written (a long curve is cut back to fit between its neighbours).</summary>
    [ObservableProperty]
    private string _curveUsed = string.Empty;

    /// <summary>The first and last station stay at the ends of the alignment.</summary>
    public bool CanEditStation => !IsEnd;

    public bool CanEditCurveLength => !IsEnd;
}
