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
using TopoAlign.Sweep.Assemblies;
using TopoAlign.Sweep.Profiles;
using TopoAlign.Sweep.Road;

namespace TopoAlign.ViewModels;

/// <summary>What the Sweep Along Alignment dialog hands back to the command.</summary>
internal sealed class SweepAlignmentRequest
{
    public SweepAlignmentRequest(Element alignment, string alignmentName, SectionProfile? profile, RoadOptions options)
    {
        Alignment = alignment;
        AlignmentName = alignmentName;
        Profile = profile;
        Options = options;
    }

    public Element Alignment { get; }
    public string AlignmentName { get; }

    /// <summary>The cross-section, already mirrored if the user asked for that.</summary>
    public SectionProfile? Profile { get; }

    public RoadOptions Options { get; }

    /// <summary>The assembly to sweep, or null for a single profile.</summary>
    public SweepAssembly? Assembly { get; set; }

    /// <summary>The section of each part's profile type, by name, when sweeping an assembly.</summary>
    public IReadOnlyDictionary<string, SectionProfile> AssemblyProfiles { get; set; } = new Dictionary<string, SectionProfile>();

    /// <summary>The toposolids to grade out from the sides of the assembly; empty to leave the ground alone.</summary>
    public IReadOnlyList<Element> Topographies { get; set; } = Array.Empty<Element>();

    public bool IsAssembly => Assembly is not null;
}
