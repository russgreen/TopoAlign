using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TopoAlign;

/// <summary>
/// A bounding box for a collection of XYZ instances.
/// The components of a tuple are read-only and cannot be changed after instantiation, so I cannot use that easily.
/// The components of an XYZ are read-only and cannot be changed except by re-instantiation, so I cannot use that easily either.
/// </summary>
public class JtBoundingBoxXyz
{
    // : Tuple<XYZ, XYZ>
    /// <summary>
    /// Array of six doubles, first three for
    /// minimum, last three for maximum values.
    /// </summary>
    private double[] _a;

    /// <summary>
    /// Initialise to infinite values.
    /// </summary>
    public JtBoundingBoxXyz()
    {
        // : base(
        // new XYZ( double.MaxValue, double.MaxValue, double.MaxValue ),
        // new XYZ( double.MinValue, double.MinValue, double.MinValue ) )
        // Min = new XYZ( double.MaxValue, double.MaxValue, double.MaxValue );
        // Max = new XYZ( double.MinValue, double.MinValue, double.MinValue );
        _a = new double[6];
        _a[0] = InlineAssignHelper(ref _a[1], InlineAssignHelper(ref _a[2], double.MaxValue));
        _a[3] = InlineAssignHelper(ref _a[4], InlineAssignHelper(ref _a[5], double.MinValue));
    }

    /// <summary>
    /// Return current lower left corner.
    /// </summary>
    public XYZ Min
    {
        get
        {
            return new XYZ(_a[0], _a[1], _a[2]);
        }
    }

    /// <summary>
    /// Return current upper right corner.
    /// </summary>
    public XYZ Max
    {
        get
        {
            return new XYZ(_a[3], _a[4], _a[5]);
        }
    }

    public XYZ MidPoint
    {
        get
        {
            return 0.5d * (Min + Max);
        }
    }


    /// <summary>
    /// Expand bounding box to contain
    /// the given new point.
    /// </summary>
    public void ExpandToContain(XYZ p)
    {
        if (p.X < _a[0])
        {
            _a[0] = p.X;
        }

        if (p.Y < _a[1])
        {
            _a[1] = p.Y;
        }

        if (p.Z < _a[2])
        {
            _a[2] = p.Z;
        }

        if (p.X > _a[3])
        {
            _a[3] = p.X;
        }

        if (p.Y > _a[4])
        {
            _a[4] = p.Y;
        }

        if (p.Z > _a[5])
        {
            _a[5] = p.Z;
        }
    }

    public static JtBoundingBoxXyz GetBoundingBoxOf(List<List<XYZ>> xyzarraylist)
    {
        var bb = new JtBoundingBoxXyz();
        foreach (List<XYZ> a in xyzarraylist)
        {
            foreach (XYZ p in a)
                bb.ExpandToContain(p);
        }

        return bb;
    }

    private static T InlineAssignHelper<T>(ref T target, T value)
    {
        target = value;
        return value;
    }
}
