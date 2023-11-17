using Autodesk.Revit.DB;
using TopoAlign.Extensions;

namespace TopoAlign.Geometry;

internal static class GeometryCalculation
{

    /// <summary>
    /// Create transformation matrix to transform points 
    /// from the global space (XYZ) to the local space of 
    /// a face (UV representation of a bounding box).
    /// Revit itself only supports Face.Transform(UV) that 
    /// translates a UV coordinate into XYZ coordinate space. 
    /// I reversed that Method to translate XYZ coords to 
    /// UV coords. At first i thought i could solve the 
    /// reverse transformation by solving a linear equation 
    /// with 2 unknown variables. But this wasn't general. 
    /// I finally found out that the transformation 
    /// consists of a displacement vector and a rotation matrix.
    /// </summary>
    public static double[,]
      CalculateMatrixForGlobalToLocalCoordinateSystem(
        Face face)
    {
        // face.Evaluate uses a rotation matrix and
        // a displacement vector to translate points

        XYZ originDisplacementVectorUV = face.Evaluate(UV.Zero);
        XYZ unitVectorUWithDisplacement = face.Evaluate(UV.BasisU);
        XYZ unitVectorVWithDisplacement = face.Evaluate(UV.BasisV);

        XYZ unitVectorU = unitVectorUWithDisplacement
          - originDisplacementVectorUV;

        XYZ unitVectorV = unitVectorVWithDisplacement
          - originDisplacementVectorUV;

        // The rotation matrix A is composed of
        // unitVectorU and unitVectorV transposed.
        // To get the rotation matrix that translates from 
        // global space to local space, take the inverse of A.

        var a11i = unitVectorU.X;
        var a12i = unitVectorU.Y;
        var a21i = unitVectorV.X;
        var a22i = unitVectorV.Y;

        return new double[2, 2] {
    { a11i, a12i },
    { a21i, a22i }};
    }

    /// <summary>
    /// Create an arc in the XY plane from a given
    /// start point, end point and radius. 
    /// </summary>
    public static Arc CreateArc2dFromRadiusStartAndEndPoint(
      XYZ ps,
      XYZ pe,
      double radius,
      bool largeSagitta = false,
      bool clockwise = false)
    {
        // https://forums.autodesk.com/t5/revit-api-forum/create-a-curve-when-only-the-start-point-end-point-amp-radius-is/m-p/7830079

        XYZ midPointChord = 0.5 * (ps + pe);
        XYZ v = pe - ps;
        double d = 0.5 * v.GetLength(); // half chord length

        // Small and large circle sagitta:
        // http://www.mathopenref.com/sagitta.html
        // https://en.wikipedia.org/wiki/Sagitta_(geometry)

        double s = largeSagitta
          ? radius + Math.Sqrt(radius * radius - d * d) // sagitta large
          : radius - Math.Sqrt(radius * radius - d * d); // sagitta small

        XYZ midPointOffset = Transform
          .CreateRotation(XYZ.BasisZ, 0.5 * Math.PI)
          .OfVector(v.Normalize().Multiply(s));

        XYZ midPointArc = clockwise
          ? midPointChord + midPointOffset
          : midPointChord - midPointOffset;

        return Arc.Create(ps, pe, midPointArc);
    }

    /// <summary>
    /// Create a new CurveLoop from a list of points.
    /// </summary>
    public static CurveLoop CreateCurveLoop(
      List<XYZ> pts)
    {
        int n = pts.Count;
        CurveLoop curveLoop = new CurveLoop();
        for (int i = 1; i < n; ++i)
        {
            curveLoop.Append(Line.CreateBound(
              pts[i - 1], pts[i]));
        }
        curveLoop.Append(Line.CreateBound(
          pts[n], pts[0]));
        return curveLoop;
    }

    public static List<XYZ> DividePoints(XYZ A, XYZ B, double length, double increment)
    {
        var retval = new List<XYZ>
        {
            A,
            B
        };

        // retval.Add(Midpoint(A, B))

        // ((Xa - Xb) / L) * Ld
        double newLength = increment;
        for (int i = 1, loopTo = (int)Math.Round(length / increment) - 1; i <= loopTo; i++)
        {
            double Xn = A.X + (B.X - A.X) / length * newLength;
            double Yn = A.Y + (B.Y - A.Y) / length * newLength;
            double Zn = A.Z + (B.Z - A.Z) / length * newLength;
            var pt = new XYZ(Xn, Yn, Zn);
            retval.Add(pt);
            newLength = newLength + increment;
        }

        return retval;
    }

    /// <summary>
    /// Return arbitrary X and Y axes for the given 
    /// normal vector according to the AutoCAD 
    /// Arbitrary Axis Algorithm
    /// https://www.autodesk.com/techpubs/autocad/acadr14/dxf/arbitrary_axis_algorithm_al_u05_c.htm
    /// </summary>
    public static void GetArbitraryAxes(
      XYZ normal,
      out XYZ ax,
      out XYZ ay)
    {
        double limit = 1.0 / 64;

        XYZ pick_cardinal_axis
          = GeometryComparison.IsZero(normal.X, limit)
            && GeometryComparison.IsZero(normal.Y, limit)
              ? XYZ.BasisY
              : XYZ.BasisZ;

        ax = pick_cardinal_axis.CrossProduct(normal).Normalize();
        ay = normal.CrossProduct(ax).Normalize();
    }

    /// <summary>
    /// Return the bottom four XYZ corners of the given 
    /// bounding box in the XY plane at the given 
    /// Z elevation in the order lower left, lower 
    /// right, upper right, upper left:
    /// </summary>
    public static XYZ[] GetBottomCorners(
      BoundingBoxXYZ b,
      double z)
    {
        return new[] {
    new XYZ( b.Min.X, b.Min.Y, z ),
    new XYZ( b.Max.X, b.Min.Y, z ),
    new XYZ( b.Max.X, b.Max.Y, z ),
    new XYZ( b.Min.X, b.Max.Y, z )
  };
    }

    /// <summary>
    /// Return the bottom four XYZ corners of the given 
    /// bounding box in the XY plane at the bb minimum 
    /// Z elevation in the order lower left, lower 
    /// right, upper right, upper left:
    /// </summary>
    public static XYZ[] GetBottomCorners(
      BoundingBoxXYZ b)
    {
        return GetBottomCorners(b, b.Min.Z);
    }

    /// <summary>
    /// Return the bounding box of a curve loop.
    /// </summary>
    public static BoundingBoxXYZ GetBoundingBox(
      CurveLoop curveLoop)
    {
        List<XYZ> pts = new List<XYZ>();
        foreach (Curve c in curveLoop)
        {
            pts.AddRange(c.Tessellate());
        }

        BoundingBoxXYZ bb = new BoundingBoxXYZ();
        bb.Clear();
        bb.ExpandToContain(pts);
        return bb;
    }

    public static bool IsBottomFace(Face f)
    {
        var b = f.GetBoundingBox();
        var p = b.Min;
        var q = b.Max;
        var midpoint = p + 0.5d * (q - p);
        var normal = f.ComputeNormal(midpoint);
        return GeometryComparison.PointsDownwards(normal);
    }

    public static bool IsTopFace(Face f)
    {
        var b = f.GetBoundingBox();
        var p = b.Min;
        var q = b.Max;
        var midpoint = p + 0.5d * (q - p);
        var normal = f.ComputeNormal(midpoint);
        return GeometryComparison.PointsUpwards(normal);
    }


    /// <summary>
    /// Return the 2D intersection point between two 
    /// unbounded lines defined in the XY plane by the 
    /// given start and end points and vectors. 
    /// Return null if the two lines are coincident,
    /// in which case the intersection is an infinite 
    /// line, or non-coincident and parallel, in which 
    /// case it is empty.
    /// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
    /// </summary>
    public static XYZ LineLineIntersection(
      XYZ p1, XYZ v1, XYZ p2, XYZ v2)
    {
        XYZ w = p2 - p1;
        XYZ p5 = null;

        double c = (v2.X * w.Y - v2.Y * w.X)
          / (v2.X * v1.Y - v2.Y * v1.X);

        if (!double.IsInfinity(c))
        {
            double x = p1.X + c * v1.X;
            double y = p1.Y + c * v1.Y;

            p5 = new XYZ(x, y, 0);
        }
        return p5;
    }

    /// <summary>
    /// Return the 2D intersection point between two 
    /// unbounded lines defined in the XY plane by the 
    /// start and end points of the two given curves. 
    /// Return null if the two lines are coincident,
    /// in which case the intersection is an infinite 
    /// line, or non-coincident and parallel, in which 
    /// case it is empty.
    /// https://en.wikipedia.org/wiki/Line%E2%80%93line_intersection
    /// </summary>
    public static XYZ LineLineIntersection(
      Curve c1,
      Curve c2)
    {
        XYZ p1 = c1.GetEndPoint(0);
        XYZ q1 = c1.GetEndPoint(1);
        XYZ p2 = c2.GetEndPoint(0);
        XYZ q2 = c2.GetEndPoint(1);
        XYZ v1 = q1 - p1;
        XYZ v2 = q2 - p2;
        return LineLineIntersection(p1, v1, p2, v2);
    }

    /// <summary>
    /// Return the 3D intersection point between
    /// a line and a plane.
    /// https://forums.autodesk.com/t5/revit-api-forum/how-can-we-calculate-the-intersection-between-the-plane-and-the/m-p/9785834
    /// https://stackoverflow.com/questions/5666222/3d-line-plane-intersection
    /// Determine the point of intersection between 
    /// a plane defined by a point and a normal vector 
    /// and a line defined by a point and a direction vector.
    /// planePoint - A point on the plane.
    /// planeNormal - The normal vector of the plane.
    /// linePoint - A point on the line.
    /// lineDirection - The direction vector of the line.
    /// lineParameter - The intersection distance along the line.
    /// Return - The point of intersection between the 
    /// line and the plane, null if the line is parallel 
    /// to the plane.
    /// </summary>
    public static XYZ LinePlaneIntersection(
      Line line,
      Plane plane,
      out double lineParameter)
    {
        XYZ planePoint = plane.Origin;
        XYZ planeNormal = plane.Normal;
        XYZ linePoint = line.GetEndPoint(0);

        XYZ lineDirection = (line.GetEndPoint(1)
          - linePoint).Normalize();

        // Is the line parallel to the plane, i.e.,
        // perpendicular to the plane normal?

        if (GeometryComparison.IsZero(planeNormal.DotProduct(lineDirection)))
        {
            lineParameter = double.NaN;
            return null;
        }

        lineParameter = (planeNormal.DotProduct(planePoint)
          - planeNormal.DotProduct(linePoint))
            / planeNormal.DotProduct(lineDirection);

        return linePoint + lineParameter * lineDirection;
    }

    /// <summary>
    /// Return the midpoint between two points.
    /// </summary>
    public static XYZ Midpoint(XYZ p, XYZ q)
    {
        return 0.5 * (p + q);
    }

    /// <summary>
    /// Return the midpoint of a Line.
    /// </summary>
    public static XYZ Midpoint(Line line)
    {
        return Midpoint(line.GetEndPoint(0),
          line.GetEndPoint(1));
    }

    /// <summary>
    /// Return the normal of a Line in the XY plane.
    /// </summary>
    public static XYZ Normal(Line line)
    {
        XYZ p = line.GetEndPoint(0);
        XYZ q = line.GetEndPoint(1);
        XYZ v = q - p;

        //Debug.Assert( IsZero( v.Z ),
        //  "expected horizontal line" );

        return v.CrossProduct(XYZ.BasisZ).Normalize();
    }

    /// <summary>
    /// Offset a list of points by a distance in a 
    /// given direction in or out of the curve loop.
    /// </summary>
    public static IEnumerable<XYZ> OffsetPoints(
      List<XYZ> pts,
      double offset,
      XYZ normal)
    {
        CurveLoop curveLoop = CreateCurveLoop(pts);

        CurveLoop curveLoop2 = CurveLoop.CreateViaOffset(
          curveLoop, offset, normal);

        return curveLoop2.Select(
            c => c.GetEndPoint(0));
    }
}