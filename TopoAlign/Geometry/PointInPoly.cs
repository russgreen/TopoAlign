using Autodesk.Revit.DB;
using System.Diagnostics;
using TopoAlign.Extensions;

namespace TopoAlign.Geometry;

public class PointInPoly
{
    /// <summary>
    /// Determine the quadrant of a polygon vertex
    /// relative to the test point.
    /// </summary>
    private static int GetQuadrant(UV vertex, UV p)
    {
        return vertex.U > p.U ? vertex.V > p.V ? 0 : 3 : vertex.V > p.V ? 1 : 2;
    }

    /// <summary>
    /// Determine the X intercept of a polygon edge
    /// with a horizontal line at the Y value of the
    /// test point.
    /// </summary>
    private static double X_intercept(UV p, UV q, double y)
    {
        Debug.Assert(0d != p.V - q.V, "unexpected horizontal segment");
        return q.U - (q.V - y) * ((p.U - q.U) / (p.V - q.V));
    }

    private static void AdjustDelta(ref int delta, UV vertex, UV next_vertex, UV p)
    {
        switch (delta)
        {
            // make quadrant deltas wrap around:
            case 3:
                {
                    delta = -1;
                    break;
                }

            case -3:
                {
                    delta = 1;
                    break;
                }
            // check if went around point cw or ccw:
            case 2:
            case -2:
                {
                    if (X_intercept(vertex, next_vertex, p.V) > p.U)
                    {
                        delta = -delta;
                    }

                    break;
                }
        }
    }

    // Robust winding-number containment (boundary-inclusive when paired with IsPointOnPolygonBoundary)
    public static bool PolygonContains(UVArray polygon, UV point)
    {
        const double eps = 1e-9;

        // Defensive: if caller hasn't already done boundary test, do it here
        // If you already call IsPointOnPolygonBoundary before this, this short-circuit is harmless.
        if (IsPointOnPolygonBoundary(polygon, point))
        {
            return true;
        }

        int winding = 0;
        int n = polygon.Size;

        // Standard winding number algorithm with upper-exclusive rule
        // j trails i (previous vertex)
        int j = n - 1;
        for (int i = 0; i < n; j = i, i++)
        {
            var vi = polygon.Item(i);
            var vj = polygon.Item(j);

            // Skip degenerate edges
            if (vi.IsAlmostEqualTo(vj))
            {
                continue;
            }

            // Upward crossing: vj.V <= y < vi.V
            if (vj.V <= point.V && vi.V > point.V)
            {
                var left = IsLeft(vj, vi, point);
                if (left > eps)
                {
                    ++winding;
                }
            }
            // Downward crossing: vi.V <= y < vj.V
            else if (vi.V <= point.V && vj.V > point.V)
            {
                var left = IsLeft(vj, vi, point);
                if (left < -eps)
                {
                    --winding;
                }
            }
        }

        // Non-zero winding rule (robust for self-consistent polygon orientation)
        return winding != 0;
    }

    private static double IsLeft(UV a, UV b, UV c)
    {
        // cross((b - a), (c - a)) in 2D
        return (b.U - a.U) * (c.V - a.V) - (c.U - a.U) * (b.V - a.V);
    }


    // New: explicit boundary check with tolerance-aware line test
    public static bool IsPointOnPolygonBoundary(UVArray polygon, UV point)
    {
        for (int i = 0, j = polygon.Size - 1; i < polygon.Size; j = i++)
        {
            var a = polygon.Item(i);
            var b = polygon.Item(j);

            // vertex hit
            if (a.IsAlmostEqualTo(point) || b.IsAlmostEqualTo(point))
                return true;

            // edge hit
            if (point.IsOnLine(a, b))
                return true;
        }
        return false;
    }

    public static bool PointInPolygon(UVArray polygon, UV point)
    {
        //// Get the angle between the point and the
        //// first and last vertices.
        //int max_point = polygon.Size - 1;
        //float total_angle = GetAngle((float)polygon.Item(max_point).U, (float)polygon.Item(max_point).V, (float)point.U, (float)point.V, (float)polygon.Item(0).U, (float)polygon.Item(0).V);

        //// Add the angles from the point
        //// to each other pair of vertices.
        //for (int i = 0, loopTo = max_point - 1; i <= loopTo; i++)
        //    total_angle += GetAngle((float)polygon.Item(i).U, (float)polygon.Item(i).V, (float)point.U, (float)point.V, (float)polygon.Item(i + 1).U, (float)polygon.Item(i + 1).V);

        //// The total angle should be 2 * PI or -2 * PI if
        //// the point is in the polygon and close to zero
        //// if the point is outside the polygon.
        //return Math.Abs(total_angle) > 0.000001d;

        bool inside = false;

        // Loop through all edges of the polygon
        for (int i = 0, j = polygon.Size - 1; i < polygon.Size; j = i++)
        {
            // Check if point is on a vertex of the polygon
            if (polygon.Item(i).IsAlmostEqualTo(point) || polygon.Item(j).IsAlmostEqualTo(point))
            {
                return true;
            }

            // Check if point is on edge of polygon
            if (point.IsOnLine(polygon.Item(i), polygon.Item(j)))
            {
                return true;
            }

            // Check if point is inside polygon
            if (((polygon.Item(i).V <= point.V && point.V < polygon.Item(j).V) || (polygon.Item(j).V <= point.V && point.V < polygon.Item(i).V)) &&
                (point.U < (polygon.Item(j).U - polygon.Item(i).U) * (point.V - polygon.Item(i).V) / (polygon.Item(j).V - polygon.Item(i).V) + polygon.Item(i).U))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static bool PointInPolygon(int[][] poly, int npoints, int xt, int yt)
    {
        int xnew, ynew;
        int xold, yold;
        int x1, y1;
        int x2, y2;
        int i;
        bool inside = false;

        if (npoints < 3)
        {
            return false;
        }

        xold = poly[npoints - 1][0];
        yold = poly[npoints - 1][1];

        for (i = 0; i < npoints; i++)
        {
            xnew = poly[i][0];
            ynew = poly[i][1];
            if (xnew > xold)
            {
                x1 = xold;
                x2 = xnew;
                y1 = yold;
                y2 = ynew;
            }
            else
            {
                x1 = xnew;
                x2 = xold;
                y1 = ynew;
                y2 = yold;
            }

            if ((xnew < xt) == (xt <= xold) && ((long)yt - (long)y1) * (long)(x2 - x1) < ((long)y2 - (long)y1) * (long)(xt - x1))
            {
                inside = !inside;
            }

            xold = xnew;
            yold = ynew;
        }
        return inside;
    }

    // Return True if the point is in the polygon.
    public static bool PointInPolygon1(PointF[] points, float X, float Y)
    {
        // Get the angle between the point and the
        // first and last vertices.
        int max_point = points.Length - 1;
        float total_angle = GetAngle(points[max_point].X, points[max_point].Y, X, Y, points[0].X, points[0].Y);



        // Add the angles from the point
        // to each other pair of vertices.
        for (int i = 0, loopTo = max_point - 1; i <= loopTo; i++)
            total_angle += GetAngle(points[i].X, points[i].Y, X, Y, points[i + 1].X, points[i + 1].Y);



        // The total angle should be 2 * PI or -2 * PI if
        // the point is in the polygon and close to zero
        // if the point is outside the polygon.
        return Math.Abs(total_angle) > 0.0000001d;
    }



    // Return the angle ABC.
    // Return a value between PI and -PI.
    // Note that the value is the opposite of what you might
    // expect because Y coordinates increase downward.
    private static float GetAngle(float Ax, float Ay, float Bx, float By, float Cx, float Cy)

    {
        float GetAngleRet = default;
        float dot_product;
        float cross_product;

        // Get the dot product and cross product.
        dot_product = DotProduct(Ax, Ay, Bx, By, Cx, Cy);
        cross_product = CrossProductLength(Ax, Ay, Bx, By, Cx, Cy);

        // Calculate the angle.
        GetAngleRet = ATan2(cross_product, dot_product);
        return GetAngleRet;
    }

    private static float DotProduct(float Ax, float Ay, float Bx, float By, float Cx, float Cy)
    {
        float DotProductRet = default;
        float BAx;
        float BAy;
        float BCx;
        float BCy;

        // Get the vectors' coordinates.
        BAx = Ax - Bx;
        BAy = Ay - By;
        BCx = Cx - Bx;
        BCy = Cy - By;

        // Calculate the dot product.
        DotProductRet = BAx * BCx + BAy * BCy;
        return DotProductRet;
    }

    private static float CrossProductLength(float Ax, float Ay, float Bx, float By, float Cx, float Cy)
    {
        float CrossProductLengthRet = default;
        float BAx;
        float BAy;
        float BCx;
        float BCy;

        // Get the vectors' coordinates.
        BAx = Ax - Bx;
        BAy = Ay - By;
        BCx = Cx - Bx;
        BCy = Cy - By;

        // Calculate the Z coordinate of the cross product.
        CrossProductLengthRet = BAx * BCy - BAy * BCx;
        return CrossProductLengthRet;
    }

    private static float ATan2(float opp, float adj)
    {
        float ATan2Ret = default;
        float angle;

        // Get the basic angle.
        if (Math.Abs(adj) < 0.0001d)
        {
            angle = (float)(Math.PI / 2d);
        }
        else
        {
            angle = (float)Math.Abs(Math.Atan(opp / adj));
        }

        // See if we are in quadrant 2 or 3.
        if (adj < 0f)
        {
            // angle > PI/2 or angle < -PI/2.
            angle = (float)(Math.PI - angle);
        }

        // See if we are in quadrant 3 or 4.
        if (opp < 0f)
        {
            angle = -angle;
        }

        // Return the result.
        ATan2Ret = angle;
        return ATan2Ret;
    }
}

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
