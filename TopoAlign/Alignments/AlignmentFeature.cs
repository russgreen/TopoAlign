using System;

namespace TopoAlign.Alignments;

/// <summary>The parts of Autodesk's Civil Alignments add-in that TopoAlign's commands rely on.</summary>
[Flags]
internal enum AlignmentFeature
{
    /// <summary>Reading alignments: Alignment.Get, GetPointAtStation and the stored JSON.</summary>
    Read = 1,

    /// <summary>Creating alignments.</summary>
    Create = 2,

    /// <summary>Changing an existing alignment.</summary>
    Update = 4,

    /// <summary>Station label sets.</summary>
    StationLabels = 8,

    All = Read | Create | Update | StationLabels
}
