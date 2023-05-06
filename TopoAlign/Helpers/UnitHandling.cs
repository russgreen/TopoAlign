using Autodesk.Revit.DB;

namespace TopoAlign.Helpers;

internal static class UnitHandling
{
    const double _inchToMm = 25.4;
    const double _footToMm = 12 * _inchToMm;
    const double _footToMeter = _footToMm * 0.001;
    const double _sqfToSqm = _footToMeter * _footToMeter;
    const double _cubicFootToCubicMeter = _footToMeter * _sqfToSqm;

    /// <summary>
    /// Hard coded abbreviations for the first 26
    /// DisplayUnitType enumeration values.
    /// </summary>
    public static string[] DisplayUnitTypeAbbreviation
      = {
  "m", // DUT_METERS = 0,
  "cm", // DUT_CENTIMETERS = 1,
  "mm", // DUT_MILLIMETERS = 2,
  "ft", // DUT_DECIMAL_FEET = 3,
  "N/A", // DUT_FEET_FRACTIONAL_INCHES = 4,
  "N/A", // DUT_FRACTIONAL_INCHES = 5,
  "in", // DUT_DECIMAL_INCHES = 6,
  "ac", // DUT_ACRES = 7,
  "ha", // DUT_HECTARES = 8,
  "N/A", // DUT_METERS_CENTIMETERS = 9,
  "y^3", // DUT_CUBIC_YARDS = 10,
  "ft^2", // DUT_SQUARE_FEET = 11,
  "m^2", // DUT_SQUARE_METERS = 12,
  "ft^3", // DUT_CUBIC_FEET = 13,
  "m^3", // DUT_CUBIC_METERS = 14,
  "deg", // DUT_DECIMAL_DEGREES = 15,
  "N/A", // DUT_DEGREES_AND_MINUTES = 16,
  "N/A", // DUT_GENERAL = 17,
  "N/A", // DUT_FIXED = 18,
  "%", // DUT_PERCENTAGE = 19,
  "in^2", // DUT_SQUARE_INCHES = 20,
  "cm^2", // DUT_SQUARE_CENTIMETERS = 21,
  "mm^2", // DUT_SQUARE_MILLIMETERS = 22,
  "in^3", // DUT_CUBIC_INCHES = 23,
  "cm^3", // DUT_CUBIC_CENTIMETERS = 24,
  "mm^3", // DUT_CUBIC_MILLIMETERS = 25,
  "l" // DUT_LITERS = 26,
      };

    /// <summary>
    /// Convert a given volume in feet to cubic meters.
    /// </summary>
    public static double CubicFootToCubicMeter(double volume)
    {
        return volume * _cubicFootToCubicMeter;
    }

    /// <summary>
    /// Convert a given length in feet to metres.
    /// </summary>
    public static double FootToMetre(double length)
    {
        return length * _footToMeter;
    }

    /// <summary>
    /// Convert a given length in feet to millimetres.
    /// </summary>
    public static double FootToMm(double length)
    {
        return length * _footToMm;
    }

    /// <summary>
    /// Convert a given length in feet to millimetres,
    /// rounded to the closest millimetre.
    /// </summary>
    public static int FootToMmInt(double length)
    {
        //return (int) ( _feet_to_mm * d + 0.5 );
        return (int)Math.Round(_footToMm * length,
          MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Convert a given length in millimetres to feet.
    /// </summary>
    public static double MmToFoot(double length)
    {
        return length / _footToMm;
    }

    /// <summary>
    /// Convert a given point or vector from millimetres to feet.
    /// </summary>
    public static XYZ MmToFoot(XYZ v)
    {
        return v.Divide(_footToMm);
    }
}