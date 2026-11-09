using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Alignments;
using TopoAlign.Sweep;

namespace TopoAlign.ViewModels;

/// <summary>A value with the name the dialog shows for it.</summary>
internal sealed class RoadChoice<T>
{
    public RoadChoice(T value, string name)
    {
        Value = value;
        Name = name;
    }

    public T Value { get; }
    public string Name { get; }
}
