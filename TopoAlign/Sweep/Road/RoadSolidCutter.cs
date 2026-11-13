using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Sweep.Road;

/// <summary>What happened to the road shapes handed to <see cref="RoadSolidCutter"/>.</summary>
internal sealed class CutOutcome
{
    public int Changed { get; set; }
    public int Unchanged { get; set; }
    public int Removed { get; set; }
    public int Extended { get; set; }
    public List<string> Warnings { get; } = new();

    public void Add(CutOutcome other)
    {
        Changed += other.Changed;
        Unchanged += other.Unchanged;
        Removed += other.Removed;
        Extended += other.Extended;
        Warnings.AddRange(other.Warnings);
    }
}

/// <summary>
/// Fits existing road solids (DirectShapes made by this add-in) to a T-junction: clears the junction area out of the main road, and
/// trims or extends the side road so it ends on the junction. All methods must be called inside a transaction.
/// </summary>
internal static class RoadSolidCutter
{
    private static readonly ILogger log = Log.ForContext(typeof(RoadSolidCutter));

    private const double MinVolume = 1e-7;      // cubic feet
    private const double Margin = 1.0;          // feet, how far a tool reaches past the road it cuts
    private const double Overlap = 0.05;        // feet, so an extension overlaps the face it grows from
    private const double HalfSpaceSize = 300.0; // feet

    /// <summary>
    /// Removes everything inside a plan polygon from the road shapes. <paramref name="polygon"/> is in internal coordinates; only X and Y are used.
    /// </summary>
    public static CutOutcome CutPolygon(Document document, IEnumerable<Element> roads, IReadOnlyList<XYZ> polygon)
    {
        var outcome = new CutOutcome();
        foreach (var road in roads)
        {
            Apply(document, road, outcome, (solid, zRange) =>
            {
                var tool = BuildPrism(polygon, zRange.Min - Margin, zRange.Max + Margin);
                return tool is null ? null : new[] { Difference(solid, tool) };
            }, extend: null);
        }

        return outcome;
    }

    /// <summary>
    /// Makes the side road end on the join line: whatever lies beyond it is cut away, and a part that stops short of it is first
    /// extended straight on from its end face.
    /// </summary>
    public static CutOutcome JoinSide(Document document, IEnumerable<Element> roads, SideJoin join)
    {
        var outcome = new CutOutcome();
        var direction = new XYZ(join.Direction.X, join.Direction.Y, 0.0);
        var need = Math.Max(Along(join.Left, direction), Along(join.Right, direction));

        foreach (var road in roads)
        {
            Apply(document, road, outcome, (solid, zRange) =>
            {
                var tool = BuildHalfSpace(join, zRange.Min - Margin, zRange.Max + Margin);
                return tool is null ? null : new[] { Difference(solid, tool) };
            }, (solid, shape) => Extend(solid, direction, need, shape, outcome));
        }

        return outcome;
    }

    private static double Along(PlanPoint p, XYZ direction) => p.X * direction.X + p.Y * direction.Y;

    private delegate IReadOnlyList<Solid?>? Operation(Solid solid, (double Min, double Max) zRange);

    private static void Apply(Document document, Element road, CutOutcome outcome, Operation operation, Func<Solid, Element, Solid>? extend)
    {
        if (road is not DirectShape shape)
        {
            outcome.Warnings.Add($"'{road.Name}' is not a plain shape, so it was left as it is.");
            return;
        }

        try
        {
            var solids = ReadSolids(shape);
            if (solids.Count == 0)
            {
                outcome.Unchanged++;
                return;
            }

            var min = double.MaxValue;
            var max = double.MinValue;
            foreach (var solid in solids)
            {
                var box = solid.GetBoundingBox();
                min = Math.Min(min, box.Transform.OfPoint(box.Min).Z);
                max = Math.Max(max, box.Transform.OfPoint(box.Max).Z);
            }

            var result = new List<GeometryObject>();
            var changed = false;
            foreach (var original in solids)
            {
                var current = original;
                if (extend is not null)
                {
                    var extended = extend(current, shape);
                    if (!ReferenceEquals(extended, current))
                    {
                        current = extended;
                        changed = true;
                    }
                }

                var pieces = operation(current, (min, max));
                if (pieces is null)
                {
                    result.Add(SolidUtils.Clone(current));
                    continue;
                }

                foreach (var piece in pieces)
                {
                    if (piece is null)
                    {
                        continue; // the whole solid was removed
                    }

                    if (piece.Volume > MinVolume)
                    {
                        result.Add(piece);
                    }
                }

                if (pieces.Count != 1 || pieces[0] is null || Math.Abs(pieces[0]!.Volume - current.Volume) > MinVolume)
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                outcome.Unchanged++;
                return;
            }

            if (result.Count == 0)
            {
                document.Delete(shape.Id);
                outcome.Removed++;
                return;
            }

            shape.SetShape(result);
            outcome.Changed++;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not fit {road} to the junction", road.Id);
            outcome.Warnings.Add($"'{road.Name}' could not be cut ({ex.Message}) and was left as it is.");
        }
    }

    private static List<Solid> ReadSolids(DirectShape shape)
    {
        var solids = new List<Solid>();
        var options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false };
        var geometry = shape.get_Geometry(options);
        if (geometry is null)
        {
            return solids;
        }

        foreach (var item in geometry)
        {
            switch (item)
            {
                case Solid solid when solid.Volume > MinVolume:
                    solids.Add(solid);
                    break;

                case GeometryInstance instance:
                    foreach (var inner in instance.GetInstanceGeometry())
                    {
                        if (inner is Solid innerSolid && innerSolid.Volume > MinVolume)
                        {
                            solids.Add(innerSolid);
                        }
                    }

                    break;
            }
        }

        return solids;
    }

    /// <summary>The solid with the tool taken away, or null if nothing is left.</summary>
    private static Solid? Difference(Solid solid, Solid tool)
    {
        var result = BooleanOperationsUtils.ExecuteBooleanOperation(solid, tool, BooleanOperationsType.Difference);
        return result is null || result.Volume <= MinVolume ? null : result;
    }

    /// <summary>Grows the solid from its end face (the planar face that looks along <paramref name="direction"/>) until it reaches <paramref name="need"/>.</summary>
    private static Solid Extend(Solid solid, XYZ direction, double need, Element shape, CutOutcome outcome)
    {
        PlanarFace? cap = null;
        var capAlong = double.MinValue;
        foreach (Face face in solid.Faces)
        {
            if (face is not PlanarFace planar || planar.FaceNormal.DotProduct(direction) < 0.9)
            {
                continue;
            }

            var along = double.MinValue;
            foreach (var loop in planar.GetEdgesAsCurveLoops())
            {
                foreach (var curve in loop)
                {
                    along = Math.Max(along, Math.Max(Along(curve.GetEndPoint(0), direction), Along(curve.GetEndPoint(1), direction)));
                }
            }

            if (along > capAlong)
            {
                capAlong = along;
                cap = planar;
            }
        }

        if (cap is null || capAlong >= need - 1e-4)
        {
            return solid;
        }

        try
        {
            var normal = cap.FaceNormal;
            var length = (need - capAlong) / Math.Max(normal.DotProduct(direction), 0.5) + Margin + Overlap;
            var back = Transform.CreateTranslation(normal.Multiply(-Overlap));
            var loops = cap.GetEdgesAsCurveLoops().Select(l => CurveLoop.CreateViaTransform(l, back)).ToList();
            var growth = GeometryCreationUtilities.CreateExtrusionGeometry(loops, normal, length);
            var union = BooleanOperationsUtils.ExecuteBooleanOperation(solid, growth, BooleanOperationsType.Union);
            if (union is not null && union.Volume > solid.Volume)
            {
                outcome.Extended++;
                return union;
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not extend {road} to the junction", shape.Id);
            outcome.Warnings.Add($"'{shape.Name}' stops short of the junction and could not be extended ({ex.Message}).");
        }

        return solid;
    }

    private static double Along(XYZ p, XYZ direction) => p.X * direction.X + p.Y * direction.Y;

    private static Solid? BuildPrism(IReadOnlyList<XYZ> polygon, double z0, double z1)
    {
        var points = polygon.Select(p => new XYZ(p.X, p.Y, z0)).ToList();
        return Extrude(points, z1 - z0);
    }

    /// <summary>A large block on the main road side of the join line.</summary>
    private static Solid? BuildHalfSpace(SideJoin join, double z0, double z1)
    {
        var dx = join.Right.X - join.Left.X;
        var dy = join.Right.Y - join.Left.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
        {
            return null;
        }

        var wx = dx / length * HalfSpaceSize;
        var wy = dy / length * HalfSpaceSize;
        var nx = join.Normal.X * HalfSpaceSize;
        var ny = join.Normal.Y * HalfSpaceSize;

        var a = new XYZ(join.Left.X - wx, join.Left.Y - wy, z0);
        var b = new XYZ(join.Right.X + wx, join.Right.Y + wy, z0);
        var c = new XYZ(b.X + nx, b.Y + ny, z0);
        var d = new XYZ(a.X + nx, a.Y + ny, z0);
        return Extrude(new List<XYZ> { a, b, c, d }, z1 - z0);
    }

    private static Solid? Extrude(IReadOnlyList<XYZ> points, double height)
    {
        // drop points too close together to make a line
        var cleaned = new List<XYZ>();
        foreach (var p in points)
        {
            if (cleaned.Count == 0 || cleaned[cleaned.Count - 1].DistanceTo(p) > 0.01)
            {
                cleaned.Add(p);
            }
        }

        while (cleaned.Count > 1 && cleaned[0].DistanceTo(cleaned[cleaned.Count - 1]) <= 0.01)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (cleaned.Count < 3)
        {
            return null;
        }

        var loop = new CurveLoop();
        for (var i = 0; i < cleaned.Count; i++)
        {
            loop.Append(Line.CreateBound(cleaned[i], cleaned[(i + 1) % cleaned.Count]));
        }

        return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, height);
    }
}
