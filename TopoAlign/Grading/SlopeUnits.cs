using TopoAlign.Alignments;

namespace TopoAlign.Grading;

internal static class SlopeUnits
{
    /// <summary>The slope as rise over run (0.333 for 1:3), always positive.</summary>
    public static double ToRiseOverRun(SlopeMode mode, double value)
    {
        if (!(value > 0.0) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The slope must be greater than zero.");
        }

        switch (mode)
        {
            case SlopeMode.Ratio:
                return 1.0 / value;

            case SlopeMode.Percent:
                return value / 100.0;

            case SlopeMode.Degrees:
                if (value >= 90.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "The angle must be less than 90 degrees.");
                }

                return Math.Tan(value * Math.PI / 180.0);

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }
}
