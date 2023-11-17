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

    /// <summary>
    /// Determine whether given 2D point lies within
    /// the polygon.
    /// 
    /// Written by Jeremy Tammik, Autodesk, 2009-09-23,
    /// based on code that I wrote back in 1996 in C++,
    /// which in turn was based on C code from the
    /// article "An Incremental Angle Point in Polygon
    /// Test" by Kevin Weiler, Autodesk, in "Graphics
    /// Gems IV", Academic Press, 1994.
    /// 
    /// Copyright (C) 2009 by Jeremy Tammik. All
    /// rights reserved.
    /// 
    /// This code may be freely used. Please preserve
    /// this comment.
    /// </summary>
    public static bool PolygonContains(UVArray polygon, UV point)
    {
        // initialize
        int quad = GetQuadrant(polygon.Item(0), point);
        int angle = 0;

        // loop on all vertices of polygon
        int next_quad;
        int delta;
        int n = polygon.Size;
        for (int i = 0, loopTo = n - 1; i <= loopTo; i++)
        {
            var vertex = polygon.Item(i);
            var next_vertex = polygon.Item(i + 1 < n ? i + 1 : 0);

            // calculate quadrant and delta from last quadrant
            next_quad = GetQuadrant(next_vertex, point);
            delta = next_quad - quad;
            AdjustDelta(ref delta, vertex, next_vertex, point);

            // add delta to total angle sum
            angle = angle + delta;

            // increment for next step
            quad = next_quad;
        }

        // complete 360 degrees (angle of + 4 or -4 ) 
        // means inside

        return angle == +4 || angle == -4;

        // odd number of windings rule:
        // if (angle & 4) return INSIDE; else return OUTSIDE;
        // non-zero winding rule:
        // if (angle != 0) return INSIDE; else return OUTSIDE;
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
        arrayPoints = new List<UV>();
        foreach (XYZ p in XYZArray)
            arrayPoints.Add(PointsUtils.Flatten(p));
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
