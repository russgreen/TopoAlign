using TopoAlign.Alignments;

namespace TopoAlign.Grading.Slope;

/// <summary>How the target slope is entered.</summary>
internal enum SlopeMode
{
    /// <summary>1 vertical to N horizontal, entered as N.</summary>
    Ratio,

    /// <summary>Rise over run as a percentage.</summary>
    Percent,

    /// <summary>Angle from horizontal in degrees.</summary>
    Degrees
}
