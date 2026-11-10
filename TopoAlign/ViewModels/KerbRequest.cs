using Nice3point.Revit.Extensions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Reflection;
using TopoAlign.Sweep;
using TopoAlign.Sweep.Kerb;
using TopoAlign.Sweep.Profiles;

namespace TopoAlign.ViewModels;

/// <summary>What the Kerb dialog hands back to the command.</summary>
internal sealed class KerbRequest
{
    public KerbRequest(IReadOnlyList<KerbChain> chains, SectionProfile profile, KerbOptions options, bool flipped, bool review)
    {
        Chains = chains;
        Profile = profile;
        Options = options;
        Flipped = flipped;
        Review = review;
    }

    /// <summary>True when the profile was flipped left to right in the dialog.</summary>
    public bool Flipped { get; }

    /// <summary>True when the kerb should be shown in the model for the user to keep, flip or cancel before it is committed.</summary>
    public bool Review { get; }

    public IReadOnlyList<KerbChain> Chains { get; }

    /// <summary>The cross-section, already mirrored if the user asked for that.</summary>
    public SectionProfile Profile { get; }

    public KerbOptions Options { get; }
}
