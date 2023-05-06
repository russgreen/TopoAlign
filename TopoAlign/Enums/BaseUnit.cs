using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign.Enums;

/// <summary>
/// Base units currently used internally by Revit.
/// </summary>
internal enum BaseUnit
{
    BU_Length = 0,         // length, feet (ft)
    BU_Angle,              // angle, radian (rad)
    BU_Mass,               // mass, kilogram (kg)
    BU_Time,               // time, second (s)
    BU_Electric_Current,   // electric current, ampere (A)
    BU_Temperature,        // temperature, kelvin (K)
    BU_Luminous_Intensity, // luminous intensity, candela (cd)
    BU_Solid_Angle,        // solid angle, steradian (sr)

    NumBaseUnits
}
