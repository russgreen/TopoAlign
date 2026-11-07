using Autodesk.Revit.DB;
using Nice3point.Revit.Extensions;
using System.Globalization;

namespace TopoAlign.Helpers;

/// <summary>Converts length values typed in a view when the user picks a different display unit.</summary>
internal static class UnitConversion
{
    /// <summary>Converts a value from one display unit to another, going through internal units (feet).</summary>
    public static decimal Convert(decimal value, ForgeTypeId from, ForgeTypeId to, int digits = 4)
    {
        if (from == to)
        {
            return value;
        }

        var converted = ((double)value).FromUnit(from).ToUnit(to);
        return decimal.Round(System.Convert.ToDecimal(converted), digits);
    }

    /// <summary>Converts text typed in a box; blank or non-numeric text is left as it is.</summary>
    public static string ConvertText(string text, ForgeTypeId from, ForgeTypeId to)
    {
        if (string.IsNullOrWhiteSpace(text) || from == to)
        {
            return text;
        }

        if (!decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            return text;
        }

        return Convert(value, from, to).ToString("0.####", CultureInfo.CurrentCulture);
    }
}
