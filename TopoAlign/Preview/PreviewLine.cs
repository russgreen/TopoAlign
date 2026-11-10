using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using TopoAlign.Alignments;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Preview;

/// <summary>One line of the pick preview in plan, with its colour (0-255 per channel).</summary>
internal readonly struct PreviewLine
{
    public PreviewLine(PlanPoint from, PlanPoint to, byte red, byte green, byte blue)
    {
        From = from;
        To = to;
        Red = red;
        Green = green;
        Blue = blue;
    }

    public PlanPoint From { get; }
    public PlanPoint To { get; }
    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }
}
