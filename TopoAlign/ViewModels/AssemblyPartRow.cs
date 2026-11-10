using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Collections.ObjectModel;
using TopoAlign.Sweep;
using TopoAlign.Sweep.Profiles;
using TopoAlign.Sweep.Road;

namespace TopoAlign.ViewModels;

/// <summary>One row of the parts grid: a part of the assembly with its values as the user types them (display units).</summary>
internal sealed partial class AssemblyPartRow : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ProfileTypeInfo? _selectedProfile;

    /// <summary>
    /// The profile the part was saved with. Kept even when the type isn't loaded in this project, so saving the assembly
    /// again doesn't lose it.
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _lateralOffset;

    [ObservableProperty]
    private decimal _verticalOffset;

    [ObservableProperty]
    private bool _mirrorProfile;

    [ObservableProperty]
    private RoadCategory? _selectedCategory;

    partial void OnSelectedProfileChanged(ProfileTypeInfo? value)
    {
        if (value is not null)
        {
            ProfileName = value.Name;
        }
    }
}
