using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign.Extensions;
internal static class UVExtensionMethods
{
    public static bool IsOnLine(this UV uv, UV uv1, UV uv2)
    {
        return (uv1.U - uv.U) * (uv2.V - uv.V) == (uv2.U - uv.U) * (uv1.V - uv.V);
    }
}
