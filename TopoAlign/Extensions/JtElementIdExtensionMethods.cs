using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign.Extensions;
internal static class JtElementIdExtensionMethods
{
    /// <summary>
    /// Predicate returning true for invalid element ids.
    /// </summary>
    public static bool IsInvalid(this ElementId id)
    {
        return ElementId.InvalidElementId == id;
    }
    /// <summary>
    /// Predicate returning true for valid element ids.
    /// </summary>
    public static bool IsValid(this ElementId id)
    {
        return !IsInvalid(id);
    }
}
