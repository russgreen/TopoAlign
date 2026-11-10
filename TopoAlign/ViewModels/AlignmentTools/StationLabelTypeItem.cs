using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Globalization;
using System.Reflection;
using TopoAlign.Alignments;

namespace TopoAlign.ViewModels.AlignmentTools;

/// <summary>An entry in the label type list: an existing type, or "create Autodesk's recommended type".</summary>
internal sealed class StationLabelTypeItem
{
    public StationLabelTypeItem(ElementId id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary><see cref="ElementId.InvalidElementId"/> means the recommended type is created when the labels are.</summary>
    public ElementId Id { get; }
    public string Name { get; }
}
