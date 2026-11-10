using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Drawing;
using TopoAlign.Extensions;

namespace TopoAlign.Geometry;

public class UVArray
{
    private List<UV> arrayPoints;

    public UVArray(List<XYZ> XYZArray)
    {
        arrayPoints = new();
        foreach (XYZ p in XYZArray)
        {
            arrayPoints.Add(PointsUtils.Flatten(p));
        }
    }

    // New: allow construction from UV lists directly
    public UVArray(List<UV> uvArray)
    {
        arrayPoints = new List<UV>(uvArray);
    }

    public UV Item(int i)
    {
        return arrayPoints[i];
    }

    public int Size
    {
        get
        {
            return arrayPoints.Count;
        }
    }
}
