using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;

namespace TopoAlign
{
    sealed class CurveGetEnpointExtension
    {
        private CurveGetEnpointExtension()
        {
        }

        public static XYZ GetEndPoint(Curve curve, int i)
        {
            return curve.GetEndPoint(i);
        }
    }


    /// <summary>
/// Curve loop utilities supporting resorting and
/// orientation of curves to form a contiguous
/// closed loop.
/// </summary>
    class CurveUtils
    {
        private const double _inch = 1.0d / 12.0d;
        private const double _sixteenth = _inch / 16.0d;

        public enum FailureCondition
        {
            Success,
            CurvesNotContigous,
            CurveLoopAboveTarget,
            NoIntersection
        }

        /// <summary>
    /// Predicate to report whether the given curve
    /// type is supported by this utility class.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>True if the curve type is supported,
    /// false otherwise.</returns>
        public static bool IsSupported(Curve curve)
        {
            return curve is Line || curve is Arc;
        }

        /// <summary>
    /// Create a new curve with the same
    /// geometry in the reverse direction.
    /// </summary>
    /// <param name="orig">The original curve.</param>
    /// <returns>The reversed curve.</returns>
    /// <throws cref="NotImplementedException">If the
    /// curve type is not supported by this utility.</throws>
        private static Curve CreateReversedCurve(Autodesk.Revit.Creation.Application creapp, Curve orig)
        {
            if (!IsSupported(orig))
            {
                throw new NotImplementedException("CreateReversedCurve for type " + orig.GetType().Name);
            }

            if (orig is Line)
            {
                return Line.CreateBound(orig.GetEndPoint(1), orig.GetEndPoint(0));
            }
            // Return creapp.NewLineBound(orig.GetEndPoint(1), orig.GetEndPoint(0))
            else if (orig is Arc)
            {
                return Arc.Create(orig.GetEndPoint(1), orig.GetEndPoint(0), orig.Evaluate(0.5d, true));
            }
            // Return creapp.NewArc(orig.GetEndPoint(1), orig.GetEndPoint(0), orig.Evaluate(0.5, True))
            else
            {
                throw new Exception("CreateReversedCurve - Unreachable");
            }
        }

        private static Curve CreateReversedCurve(Curve orig)
        {
            if (!IsSupported(orig))
            {
                throw new NotImplementedException("CreateReversedCurve for type " + orig.GetType().Name);
            }

            if (orig is Line)
            {
                return Line.CreateBound(orig.GetEndPoint(1), orig.GetEndPoint(0));
            }
            else if (orig is Arc)
            {
                return Arc.Create(orig.GetEndPoint(1), orig.GetEndPoint(0), orig.Evaluate(0.5d, true));
            }
            else
            {
                throw new Exception("CreateReversedCurve - Unreachable");
            }
        }

        /// <summary>
    /// Sort a list of curves to make them correctly
    /// ordered and oriented to form a closed loop.
    /// </summary>
        public static void SortCurvesContiguous(Autodesk.Revit.Creation.Application creapp, IList<Curve> curves, bool debug_output)
        {
            int n = curves.Count;

            // Walk through each curve (after the first) 
            // to match up the curves in order

            for (int i = 0, loopTo = n - 1; i <= loopTo; i++)
            {
                var curve = curves[i];
                var endPoint = curve.GetEndPoint(1);
                if (debug_output)
                {
                    Debug.Print("{0} endPoint {1}", i, Util.PointString(endPoint));
                }

                XYZ p;

                // Find curve with start point = end point

                bool found = i + 1 >= n;
                for (int j = i + 1, loopTo1 = n - 1; j <= loopTo1; j++)
                {
                    p = curves[j].GetEndPoint(0);

                    // If there is a match end->start, 
                    // this is the next curve

                    if (_sixteenth > p.DistanceTo(endPoint))
                    {
                        if (debug_output)
                        {
                            Debug.Print("{0} start point, swap with {1}", j, i + 1);
                        }

                        if (i + 1 != j)
                        {
                            var tmp = curves[i + 1];
                            curves[i + 1] = curves[j];
                            curves[j] = tmp;
                        }

                        found = true;
                        break;
                    }

                    p = curves[j].GetEndPoint(1);

                    // If there is a match end->end, 
                    // reverse the next curve

                    if (_sixteenth > p.DistanceTo(endPoint))
                    {
                        if (i + 1 == j)
                        {
                            if (debug_output)
                            {
                                Debug.Print("{0} end point, reverse {1}", j, i + 1);
                            }

                            curves[i + 1] = CreateReversedCurve(creapp, curves[j]);
                        }
                        else
                        {
                            if (debug_output)
                            {
                                Debug.Print("{0} end point, swap with reverse {1}", j, i + 1);
                            }

                            var tmp = curves[i + 1];
                            curves[i + 1] = CreateReversedCurve(creapp, curves[j]);
                            curves[j] = tmp;
                        }

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new Exception("SortCurvesContiguous:" + " non-contiguous input curves");
                }
            }
        }

        public static void SortCurvesContiguous(IList<Curve> curves)
        {
            double _precision1 = 1.0d / 12.0d / 16.0d;
            // around 0.00520833
            double _precision2 = 0.001d;
            // limit for CurveLoop.Create(...)
            int n = curves.Count;

            // Walk through each curve (after the first)
            // to match up the curves in order

            for (int i = 0, loopTo = n - 1; i <= loopTo; i++)
            {
                var curve = curves[i];
                var beginPoint = curve.GetEndPoint(0);
                var endPoint = curve.GetEndPoint(1);
                XYZ p;
                XYZ q;

                // Find curve with start point = end point

                bool found = i + 1 >= n;
                for (int j = i + 1, loopTo1 = n - 1; j <= loopTo1; j++)
                {
                    p = curves[j].GetEndPoint(0);
                    q = curves[j].GetEndPoint(1);

                    // If there is a match end->start,
                    // this is the next curve
                    if (p.DistanceTo(endPoint) < _precision1)
                    {
                        if (p.DistanceTo(endPoint) > _precision2)
                        {
                            var intermediate = new XYZ((endPoint.X + p.X) / 2.0d, (endPoint.Y + p.Y) / 2.0d, (endPoint.Z + p.Z) / 2.0d);
                            curves[i] = Line.CreateBound(beginPoint, intermediate);
                            curves[j] = Line.CreateBound(intermediate, q);
                        }

                        if (i + 1 != j)
                        {
                            var tmp = curves[i + 1];
                            curves[i + 1] = curves[j];
                            curves[j] = tmp;
                        }

                        found = true;
                        break;
                    }

                    // If there is a match end->end,
                    // reverse the next curve

                    if (q.DistanceTo(endPoint) < _precision1)
                    {
                        if (q.DistanceTo(endPoint) > _precision2)
                        {
                            var intermediate = new XYZ((endPoint.X + q.X) / 2.0d, (endPoint.Y + q.Y) / 2.0d, (endPoint.Z + q.Z) / 2.0d);
                            curves[i] = Line.CreateBound(beginPoint, intermediate);
                            curves[j] = Line.CreateBound(p, intermediate);
                        }

                        if (i + 1 == j)
                        {
                            curves[i + 1] = CreateReversedCurve(curves[j]);
                        }
                        else
                        {
                            var tmp = curves[i + 1];
                            curves[i + 1] = CreateReversedCurve(curves[j]);
                            curves[j] = tmp;
                        }

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new Exception("SortCurvesContiguous :" + " non-contiguous input curves");
                }
            }
        }

        /// <summary>
    /// Return a list of curves which are correctly
    /// ordered and oriented to form a closed loop.
    /// </summary>
    /// <param name="doc">The document.</param>
    /// <param name="boundaries">The list of curve element references which are the boundaries.</param>
    /// <returns>The list of curves.</returns>
        public static IList<Curve> GetContiguousCurvesFromSelectedCurveElements(Document doc, IList<Reference> boundaries, bool debug_output)
        {
            var curves = new List<Curve>();

            // Build a list of curves from the curve elements

            foreach (Reference reference in boundaries)
            {
                CurveElement curveElement = doc.GetElement(reference) as CurveElement;
                curves.Add(curveElement.GeometryCurve.Clone());
            }

            SortCurvesContiguous(doc.Application.Create, curves, debug_output);
            return curves;
        }

        /// <summary>
    /// Identifies if the curve lies entirely in an XY plane (Z = constant)
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>True if the curve lies in an XY plane, false otherwise.</returns>
        public static bool IsCurveInXYPlane(Curve curve)
        {
            // quick reject - are endpoints at same Z

            double zDelta = curve.GetEndPoint(1).Z - curve.GetEndPoint(0).Z;
            if (Math.Abs(zDelta) > 0.00001d)
            {
                return false;
            }

            if (!(curve is Line) && !curve.IsCyclic)
            {
                // Create curve loop from curve and 
                // connecting line to get plane

                var curves = new List<Curve>();
                curves.Add(curve);

                // curves.Add(Line.CreateBound(curve.GetEndPoint(1), curve.GetEndPoint(0)));

                var curveLoop__1 = CurveLoop.Create(curves);
                var normal = curveLoop__1.GetPlane().Normal.Normalize();
                if (!normal.IsAlmostEqualTo(XYZ.BasisZ) && !normal.IsAlmostEqualTo(XYZ.BasisZ.Negate()))
                {
                    return false;
                }
            }

            return true;
        }
    }
}