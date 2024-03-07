using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Reflection;

namespace TopoAlign.Helpers;

public class StringFormatting
{

    /// <summary>
    /// Return a string representation in degrees
    /// for an angle given in radians.
    /// </summary>
    public static string AngleString(double angle)
    {
        return RealString(angle * 180 / Math.PI)
          + " degrees";
    }

    /// <summary>
    /// Return a string for this bounding box
    /// with its coordinates formatted to two
    /// decimal places.
    /// </summary>
    public static string BoundingBoxString(
      BoundingBoxUV bb,
      bool onlySpaceSeparator = false)
    {
        string format_string = onlySpaceSeparator
          ? "{0} {1}"
          : "({0},{1})";

        return string.Format(format_string,
          PointString(bb.Min, onlySpaceSeparator),
          PointString(bb.Max, onlySpaceSeparator));
    }

    /// <summary>
    /// Return a dot (full stop) for zero
    /// or a colon for more than zero.
    /// </summary>
    public static string DotOrColon(int n)
    {
        return 0 < n ? ":" : ".";
    }

    /// <summary>
    /// Return a hash string for a real number
    /// formatted to nine decimal places.
    /// </summary>
    public static string HashString(double a)
    {
        return a.ToString("0.#########");
    }

    /// <summary>
    /// Return a hash string for an XYZ point
    /// or vector with its coordinates
    /// formatted to nine decimal places.
    /// </summary>
    public static string HashString(XYZ p)
    {
        return string.Format("({0},{1},{2})",
          HashString(p.X),
          HashString(p.Y),
          HashString(p.Z));
    }

    /// <summary>
    /// Return a string for a length in millimetres
    /// formatted as an integer value.
    /// </summary>
    public static string MmString(double length)
    {
        //return RealString( FootToMm( length ) ) + " mm";

#if REVIT2021_OR_GREATER
        return $"{Math.Round(UnitUtils.ConvertFromInternalUnits(length, UnitTypeId.Millimeters))} mm";
#else
        return $"{Math.Round(UnitUtils.ConvertFromInternalUnits(length, DisplayUnitType.DUT_MILLIMETERS))} mm";
#endif


    }

#if REVIT2022_OR_GREATER
    /// <summary>
    /// List all Forge type ids
    /// </summary>
    /// <param name="doc"></param>
    public static void ListForgeTypeIds()
    {
        //ForgeTypeId a = SpecTypeId.Acceleration;
        //Debug.Print( a.TypeId );

        Type spityp = typeof(SpecTypeId);

        //foreach( MemberInfo mi in spityp.GetMembers() )
        //{
        //  Debug.Print( mi.Name );
        //}

        PropertyInfo[] ps = spityp.GetProperties(
          BindingFlags.Public | BindingFlags.Static);

        // Sort properties alphabetically by name 

        Array.Sort(ps,
          delegate (PropertyInfo p1, PropertyInfo p2)
          { return p1.Name.CompareTo(p2.Name); });

        Debug.Print("{0} properties:", ps.Length);

        foreach (PropertyInfo pi in ps)
        {
            if (pi.PropertyType == typeof(ForgeTypeId))
            {
                object obj = pi.GetValue(null, null);

                ForgeTypeId fti = obj as ForgeTypeId;

                Debug.Print("{0}: {1}", pi.Name, fti.TypeId);
            }
        }

        IList<ForgeTypeId> specs = UnitUtils.GetAllMeasurableSpecs();

        Debug.Print("{0} specs:", specs.Count);

        foreach (ForgeTypeId fti in specs)
        {
            Debug.Print("{0}: {1}, {2}",
              fti, fti.TypeId,
              UnitUtils.GetTypeCatalogStringForSpec(fti));
        }

        IList<ForgeTypeId> units = UnitUtils.GetAllUnits();

        Debug.Print("{0} units:", units.Count);

        foreach (ForgeTypeId fti in units)
        {
            Debug.Print("{0}: {1}, {2}",
              fti, fti.TypeId,
              UnitUtils.GetTypeCatalogStringForUnit(fti));
        }
    }

#endif


    /// <summary>
    /// Return an English plural suffix for the given
    /// number of items, i.e. 's' for zero or more
    /// than one, and nothing for exactly one.
    /// </summary>
    public static string PluralSuffix(int n)
    {
        return 1 == n ? "" : "s";
    }

    /// <summary>
    /// Return an English plural suffix 'ies' or
    /// 'y' for the given number of items.
    /// </summary>
    public static string PluralSuffixY(int n)
    {
        return 1 == n ? "y" : "ies";
    }

    /// <summary>
    /// Return a string for this point array
    /// with its coordinates formatted to two
    /// decimal places.
    /// </summary>
    public static string PointArrayString(
      IEnumerable<XYZ> pts,
      bool onlySpaceSeparator = false)
    {
        string separator = onlySpaceSeparator
          ? " "
          : ", ";

        return string.Join(separator,
          pts.Select(p
           => PointString(p, onlySpaceSeparator)));
    }

    /// <summary>
    /// Return a string for a UV point
    /// or vector with its coordinates
    /// formatted to two decimal places.
    /// </summary>
    public static string PointString(
      UV p,
      bool onlySpaceSeparator = false)
    {
        string format_string = onlySpaceSeparator
          ? "{0} {1}"
          : "({0},{1})";

        return string.Format(format_string,
          RealString(p.U),
          RealString(p.V));
    }

    /// <summary>
    /// Return a string for an XYZ point
    /// or vector with its coordinates
    /// formatted to two decimal places.
    /// </summary>
    public static string PointString(
      XYZ p,
      bool onlySpaceSeparator = false)
    {
        string format_string = onlySpaceSeparator
          ? "{0} {1} {2}"
          : "({0},{1},{2})";

        return string.Format(format_string,
          RealString(p.X),
          RealString(p.Y),
          RealString(p.Z));
    }

    /// <summary>
    /// Return a string for a real number
    /// formatted to two decimal places.
    /// </summary>
    public static string RealString(double a)
    {
        return a.ToString("0.##");
    }
}