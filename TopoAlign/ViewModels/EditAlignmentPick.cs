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

/// <summary>What the dialog was hidden for, so the command knows which pick to run.</summary>
internal enum EditAlignmentPick
{
    None,
    Alignment,
    MovePoint,
    AddPoint
}
