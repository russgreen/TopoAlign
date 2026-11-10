using Autodesk.Revit.DB;
using Serilog;
using TopoAlign.Alignments;
using TopoAlign.Geometry.Topography;
using TopoAlign.Services;

namespace TopoAlign.Grading.TopoEdge;

/// <summary>Reads two topographies and adds points to them so the edge they share lines up.</summary>
internal static class TopoEdgeAlignerRevit
{
    private static readonly ILogger log = Log.ForContext(typeof(TopoEdgeAlignerRevit));

    /// <summary>Must be called from within the Revit API context.</summary>
    public static AlignTopoEdgesResult Apply(Document document, Element first, Element second, bool both, ProgressService? progress = null)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (first is null)
        {
            throw new ArgumentNullException(nameof(first));
        }

        if (second is null)
        {
            throw new ArgumentNullException(nameof(second));
        }

        if (first.Id == second.Id)
        {
            throw new InvalidOperationException("Pick two different topographies.");
        }

        var steps = both ? 3 : 2;
        progress?.Restart(steps, "Step 1 of " + steps + ": reading the edges...");
        var alignment = TopoEdgeAligner.Align(ReadOutline(first), ReadOutline(second), both, firstPoints: ReadExistingPoints(first));
        progress?.SetValue(1);

        var result = new AlignTopoEdgesResult
        {
            SharedPoints = alignment.SharedPoints
        };

        progress?.ThrowIfCancelled();
        progress?.Report("Step 2 of " + steps + ": updating the first topography...");
        (result.MovedOnFirst, result.AddedToFirst) = EditPoints(document, first, alignment.AddToFirst, alignment.ChangeOnFirst);
        progress?.SetValue(2);
        if (both)
        {
            // no cancel check here: the first topography is already changed, and stopping now would leave only half the job done
            progress?.Report("Step 3 of " + steps + ": updating the second topography...");
            result.AddedToSecond = EditPoints(document, second, alignment.AddToSecond, Array.Empty<GradePoint>()).Added;
        }

        return result;
    }

    private static List<GradePoint> ReadExistingPoints(Element topography)
    {
#if REVIT2024_OR_GREATER
        var editor = ((Toposolid)topography).GetSlabShapeEditor();
        return editor.IsEnabled
            ? editor.SlabShapeVertices.Cast<SlabShapeVertex>().Select(vertex => vertex.Position)
                .Select(point => new GradePoint(point.X, point.Y, point.Z)).ToList()
            : new List<GradePoint>();
#else
        return ((Autodesk.Revit.DB.Architecture.TopographySurface)topography).GetPoints()
            .Select(point => new GradePoint(point.X, point.Y, point.Z)).ToList();
#endif
    }

    internal static List<EdgeSegment> ReadOutline(Element topography)
    {
        var triangles = new List<(GradePoint, GradePoint, GradePoint)>();
        TopographyReader.ForEachUpFacingTriangle(topography, (a, b, c) =>
            triangles.Add((new GradePoint(a.X, a.Y, a.Z), new GradePoint(b.X, b.Y, b.Z), new GradePoint(c.X, c.Y, c.Z))));

        if (triangles.Count == 0)
        {
            throw new InvalidOperationException("No surface geometry could be read from the selected topography.");
        }

        return TopoEdgeAligner.BoundarySegments(triangles);
    }

    // an existing point this close in plan is the one to move
    private const double MatchTolerance = 0.02;

    /// <summary>Moves the given existing points to their new heights, then adds the new points. Returns how many points were moved and added.</summary>
    private static (int Moved, int Added) EditPoints(Document document, Element topography, IReadOnlyList<GradePoint> additions, IReadOnlyList<GradePoint> changes)
    {
        if (additions.Count == 0 && changes.Count == 0)
        {
            return (0, 0);
        }

        var xyz = additions.Select(p => new XYZ(p.X, p.Y, p.Z)).ToList();
        var moved = 0;

        try
        {
#if REVIT2024_OR_GREATER
            using var transaction = new Transaction(document, "Align topo edges");
            transaction.Start();

            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new FailureHandler());
            transaction.SetFailureHandlingOptions(options);

            // a toposolid that hasn't had its shape edited yet has to have the editor switched on first
            var editor = ((Toposolid)topography).GetSlabShapeEditor();
            if (!editor.IsEnabled)
            {
                editor.Enable();
                document.Regenerate();
            }

            moved = MoveVertices(document, editor, changes);

            if (xyz.Count > 0)
            {
                editor.AddPoints(xyz);
            }

            if (transaction.Commit() != TransactionStatus.Committed)
            {
                throw new InvalidOperationException("Revit would not accept the new points.");
            }
#else
            var surface = (Autodesk.Revit.DB.Architecture.TopographySurface)topography;
            using var scope = new Autodesk.Revit.DB.Architecture.TopographyEditScope(document, "Align topo edges");
            scope.Start(surface.Id);

            using (var transaction = new Transaction(document, "Edit points"))
            {
                transaction.Start();

                // a point can't be moved, so the old one is replaced by one at the new height
                var existing = surface.GetPoints();
                var remove = new List<XYZ>();
                foreach (var change in changes)
                {
                    var nearest = existing
                        .Select(p => (Point: p, Distance: Math.Sqrt((p.X - change.X) * (p.X - change.X) + (p.Y - change.Y) * (p.Y - change.Y))))
                        .Where(p => p.Distance <= MatchTolerance)
                        .OrderBy(p => p.Distance)
                        .Select(p => p.Point)
                        .FirstOrDefault();
                    if (nearest is null)
                    {
                        throw new InvalidOperationException("An existing boundary point could not be found for alignment.");
                    }
                    remove.Add(nearest);
                    xyz.Add(new XYZ(nearest.X, nearest.Y, change.Z));
                    moved++;
                }

                if (remove.Count > 0)
                {
                    surface.DeletePoints(remove);
                }

                if (xyz.Count > 0)
                {
                    surface.AddPoints(xyz);
                }

                transaction.Commit();
            }

            scope.Commit(new FailureHandler());
#endif
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Editing points of {topography} failed", topography.Id);
            throw;
        }

        return (moved, additions.Count);
    }

#if REVIT2024_OR_GREATER
    /// <summary>
    /// Sets the height of the existing vertex at each place. The elevation <c>ModifySubElement</c> takes may be measured from somewhere other than
    /// the model's zero, so the first change is checked against where the vertex ends up and the difference is applied to the rest.
    /// </summary>
    internal static int MoveVertices(Document document, SlabShapeEditor editor, IReadOnlyList<GradePoint> changes)
    {
        var moved = 0;
        var offset = 0.0;
        var calibrated = false;

        foreach (var change in changes)
        {
            var vertex = NearestVertex(editor, change);
            if (vertex is null)
            {
                // The outline has a corner here but the shape editor holds no vertex there (for example a toposolid made from a road,
                // whose boundary corners aren't shape vertices). Put a point at the right height instead of moving one.
                try
                {
                    editor.AddPoints(new List<XYZ> { new XYZ(change.X, change.Y, change.Z) });
                    moved++;
                    continue;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"An existing boundary vertex could not be found for alignment at {change.X:0.###}, {change.Y:0.###}, " +
                        $"and a point could not be added there either ({ex.Message}). The toposolid has {editor.SlabShapeVertices.Size} shape vertices.", ex);
                }
            }

            var before = vertex.Position.Z;
            if (Math.Abs(before - change.Z) <= 1e-6)
            {
                // already at the right height
                moved++;
                continue;
            }

            // Until the first move shows how the elevation is measured, assume it is relative to the surface the vertex is on now.
            // If it is really absolute the vertex only lands a little off (by the surface's height), and the calibration below corrects it,
            // whereas guessing absolute on a toposolid that sits high would throw the corner far off, which Revit can refuse.
            var argument = calibrated ? change.Z - offset : change.Z - before;

            try
            {
                editor.ModifySubElement(vertex, argument);
                moved++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not align the boundary vertex at {change.X}, {change.Y}.", ex);
            }

            if (calibrated)
            {
                continue;
            }

            calibrated = true;
            document.Regenerate();
            var after = NearestVertex(editor, change);
            if (after is null)
            {
                throw new InvalidOperationException(
                    $"The moved boundary vertex at {change.X:0.###}, {change.Y:0.###} could not be verified; {DescribeNearest(editor, change)}.");
            }

            var actual = after.Position.Z;
            offset = actual - argument;
            if (Math.Abs(actual - change.Z) > 1e-6)
            {
                editor.ModifySubElement(after, change.Z - offset);
            }
        }

        document.Regenerate();
        foreach (var change in changes)
        {
            var vertex = NearestVertex(editor, change);
            if (vertex is null || Math.Abs(vertex.Position.Z - change.Z) > TopoEdgeAligner.HeightTolerance)
            {
                throw new InvalidOperationException("Revit did not align every boundary vertex to the reference edge.");
            }
        }
        return moved;
    }

    private static string DescribeNearest(SlabShapeEditor editor, GradePoint place)
    {
        var best = double.MaxValue;
        XYZ? at = null;
        foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
        {
            var dx = vertex.Position.X - place.X;
            var dy = vertex.Position.Y - place.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < best)
            {
                best = distance;
                at = vertex.Position;
            }
        }

        return at is null
            ? "the toposolid has no shape vertices"
            : $"the nearest of {editor.SlabShapeVertices.Size} vertices is {best:0.###} ft away at {at.X:0.###}, {at.Y:0.###}, {at.Z:0.###}";
    }

    internal static SlabShapeVertex? NearestVertex(SlabShapeEditor editor, GradePoint place)
    {
        SlabShapeVertex? nearest = null;
        var best = MatchTolerance;
        foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
        {
            var dx = vertex.Position.X - place.X;
            var dy = vertex.Position.Y - place.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= best)
            {
                best = distance;
                nearest = vertex;
            }
        }

        return nearest;
    }
#endif
}