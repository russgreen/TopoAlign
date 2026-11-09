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

namespace TopoAlign.ViewModels;

/// <summary>What the Edit Profile dialog hands back to the command: the alignment and its edited JSON.</summary>
internal sealed class EditProfileRequest
{
    public EditProfileRequest(Element alignment, string name, string json, double profileLength, int stationsAdded, int stationsRemoved, int stationsChanged)
    {
        Alignment = alignment;
        Name = name;
        Json = json;
        ProfileLength = profileLength;
        StationsAdded = stationsAdded;
        StationsRemoved = stationsRemoved;
        StationsChanged = stationsChanged;
    }

    public Element Alignment { get; }
    public string Name { get; }
    public string Json { get; }

    /// <summary>Length of the alignment in feet (internal units).</summary>
    public double ProfileLength { get; }

    public int StationsAdded { get; }
    public int StationsRemoved { get; }

    /// <summary>Existing stations whose station, elevation or curve length was changed.</summary>
    public int StationsChanged { get; }
}
