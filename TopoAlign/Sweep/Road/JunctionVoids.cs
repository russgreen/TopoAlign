using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Serilog;
using TopoAlign.Alignments.Plan;

namespace TopoAlign.Sweep.Road;

/// <summary>
/// Cuts a T-junction out of road parts that are family instances by putting a void into each part's family, then loading the family back.
/// The void is an ordinary void extrusion in the family: it can be edited in the Family Editor, and deleting it puts the road back.
/// Each void is marked with the side road it belongs to, so a junction's voids can be found again and replaced or removed.
/// </summary>
internal static class JunctionVoids
{
    private static readonly ILogger log = Log.ForContext(typeof(JunctionVoids));

    private const double SideWidthPad = 25.0; // feet the side road's void reaches past the kerb lines, to take in footways and verges
    private const double SideDepthPad = 30.0; // feet the side road's void reaches past where the road ended
    private const double Margin = 1.0; // feet the void reaches past the road it cuts

    private static readonly Guid VoidSchemaId = new("5d1c9a42-7b3e-4f0a-9c61-2e8b4a7d1f30");
    private static readonly Guid RecordSchemaId = new("a83f6e15-2c47-4b9d-8e10-6d5c0b9a3f72");

    private const string SideField = "Side";
    private const string FamiliesField = "Families";

    // A void's own data: enough to put the void back after the family has been restored from its original.
    private static readonly Guid VoidDataSchemaId = new("e17a5d33-9b20-4c8e-a6f4-0d83b1c52e69");
    private const string RoleField = "Role";
    private const string PointsField = "Points";
    private const string HeightField = "Height";

    private static Schema VoidDataSchema() => Schema.Lookup(VoidDataSchemaId)
        ?? BuildSchema(VoidDataSchemaId, "TopoAlignJunctionVoidData", SideField, RoleField, PointsField, HeightField);

    private static Schema VoidSchema() => Schema.Lookup(VoidSchemaId) ?? BuildSchema(VoidSchemaId, "TopoAlignJunctionVoid", SideField);

    private static Schema RecordSchema() => Schema.Lookup(RecordSchemaId) ?? BuildSchema(RecordSchemaId, "TopoAlignJunctionRecord", SideField, FamiliesField);

    private static Schema BuildSchema(Guid id, string name, params string[] stringFields)
    {
        var builder = new SchemaBuilder(id);
        builder.SetSchemaName(name);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        foreach (var field in stringFields)
        {
            builder.AddSimpleField(field, typeof(string));
        }

        return builder.Finish();
    }

    /// <summary>
    /// Puts the junction's voids into the road part families, replacing any an earlier run of the same junction made. Must be called
    /// with no transaction open: it edits and reloads families. Follow it with <see cref="SaveRecord"/> inside a transaction.
    /// Parts that are not family instances are not touched here. Returns the families that now hold a void.
    /// </summary>
    public static List<string> Apply(Document document, string junctionKey, IReadOnlyList<FamilyInstance> mainParts,
        IReadOnlyList<XYZ>? cutPolygon, IReadOnlyList<FamilyInstance> sideParts, SideJoin? join, CutOutcome outcome)
    {
        var side = junctionKey;

        // Reloading a family can leave the element objects held from before it stale, so everything a job needs is read here, up
        // front, as plain values, and elements are fetched again by id when they are used.
        var jobs = new List<VoidJob>();
        if (cutPolygon is not null)
        {
            jobs.AddRange(mainParts.Select(p => MakeJob(p, cutPolygon, "main road")).OfType<VoidJob>());
        }

        if (join is { } line && sideParts.Count > 0 && SideVoidPolygon(sideParts, line, outcome) is { } quad)
        {
            jobs.AddRange(sideParts.Select(p => MakeJob(p, quad, "side road")).OfType<VoidJob>());
        }

        var record = ReadRecord(document, side);

        // each family to cut is done once; families an earlier run put voids in that this run no longer cuts are cleaned up
        var done = new HashSet<string>();
        var withVoid = new List<string>();
        foreach (var job in jobs)
        {
            if (!done.Add(job.FamilyName))
            {
                continue;
            }

            if (EditFamily(document, job.FamilyName, job, side, outcome))
            {
                withVoid.Add(job.FamilyName);
            }
        }

        foreach (var id in record.Where(name => !done.Contains(name)))
        {
            EditFamily(document, id, null, side, outcome);
        }

        return withVoid;
    }

    /// <summary>Takes the junction's voids out of every family that holds one. Must be called with no transaction open.</summary>
    public static int RemoveVoids(Document document, string junctionKey, CutOutcome outcome)
    {
        var side = junctionKey;
        var removed = 0;
        foreach (var id in ReadRecord(document, side))
        {
            if (EditFamily(document, id, null, side, outcome))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Opens a family, takes out the voids this junction put there, adds a new one if <paramref name="polygon"/> is given, and loads the
    /// family back. Returns true if the family now holds the junction's void (or, with no polygon, if a void was taken out).
    /// </summary>
    private static bool EditFamily(Document document, string familyName, VoidJob? job, string side, CutOutcome outcome)
    {
        // fetched again here, as the family may have been loaded again since it was last looked at
        if (FindFamily(document, familyName) is not { } family || !family.IsValidObject)
        {
            return false;
        }

        if (!family.IsEditable)
        {
            outcome.Warnings.Add($"The family '{familyName}' can't be edited, so it was left as it is.");
            return false;
        }

        // What the family holds now: the voids of every junction that has cut it.
        List<StoredVoid> current;
        bool hadOwn;
        var unknownOthers = 0;
        Document? reading = null;
        try
        {
            reading = document.EditFamily(family);
            current = ReadVoids(reading, side, out hadOwn, out unknownOthers);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not read the family {family} for the junction", familyName);
            outcome.Warnings.Add($"'{familyName}' could not be read ({ex.GetType().Name}: {ex.Message}) and was left as it is.");
            return false;
        }
        finally
        {
            reading?.Close(false);
        }

        // the voids of other junctions stay; this junction's is replaced by the new one, or goes
        // (the project's own records of the cuts come first: they don't depend on the family still carrying the voids' data)
        var wanted = ReadCuts(document, familyName).Where(v => v.Side != side).ToList();
        wanted.AddRange(current.Where(v => v.Side != side && wanted.All(w => w.Side != v.Side)));
        if (job is not null)
        {
            wanted.Add(new StoredVoid(side, job.Role, job.Polygon.Select(p => job.ToFamily.OfPoint(new XYZ(p.X, p.Y, job.MinZ - Margin))).ToList(), job.MaxZ - job.MinZ + 2.0 * Margin));
        }

        if (unknownOthers > 0)
        {
            outcome.Warnings.Add($"'{familyName}' holds a junction made by an earlier version, whose cut can't be made again. Run that junction again to cut it.");
        }

        // The one copy of the family from before any junction cut it. Cuts made directly can't be undone, so the family is put back from
        // that copy and the voids of the other junctions are cut again.
        var stored = ReadBackup(document, familyName);
        if (job is null && !hadOwn && stored is null)
        {
            return false;
        }

        if (stored is not null)
        {
            if (!LoadFamilyFile(document, familyName, Convert.FromBase64String(stored), outcome))
            {
                return false;
            }

            if (wanted.Count == 0)
            {
                using var drop = new Transaction(document, "Junction backup");
                drop.Start();
                DeleteBackups(document, familyName);
                DeleteCut(document, familyName, side);
                drop.Commit();
                return true;
            }
        }
        else if (job is null)
        {
            outcome.Warnings.Add($"'{familyName}' has no stored copy, so only its void was removed. If it was cut directly, make the road again to restore it.");
        }

        Document? familyDocument = null;
        try
        {
            if (FindFamily(document, familyName) is not { } latest)
            {
                return false;
            }

            familyDocument = document.EditFamily(latest);
            var schema = VoidDataSchema();
            var legacy = VoidSchema();

            // the family as it is before any junction touches it
            var original = job is not null && stored is null ? CaptureFamily(familyDocument, familyName) : null;
            if (job is not null && stored is null && original is null)
            {
                outcome.Warnings.Add($"A copy of '{familyName}' could not be kept, so if it has to be cut directly Remove T-Junction can't restore it.");
            }

            var cutDirectly = false;

            using var transaction = new Transaction(familyDocument, "Junction void");
            transaction.Start();

            var earlier = new FilteredElementCollector(familyDocument)
                .OfClass(typeof(Extrusion))
                .Where(e => (e.GetEntity(schema) is { } entity && entity.IsValid())
                            || (e.GetEntity(legacy) is { } old && old.IsValid() && old.Get<string>(SideField) == side))
                .Select(e => e.Id)
                .ToList();
            if (earlier.Count > 0)
            {
                familyDocument.Delete(earlier);
            }

            foreach (var item in wanted)
            {
                var again = item.Side == side && job is not null
                    ? job
                    : new VoidJob(familyName, item.Points, item.Role, item.Points[0].Z + Margin, item.Points[0].Z + item.Height - Margin, Transform.Identity);
                cutDirectly |= AddVoid(familyDocument, schema, again, item.Side, outcome);
            }

            transaction.Commit();
            familyDocument.LoadFamily(document, new FamilyOverwriteOptions());

            // what each junction cut is kept in the project, so the cuts of the others can be made again when one goes
            try
            {
                using var cuts = new Transaction(document, "Junction cuts");
                cuts.Start();
                DeleteCut(document, familyName, side);
                if (wanted.FirstOrDefault(v => v.Side == side) is { } mine && job is not null)
                {
                    SaveCut(document, familyName, mine);
                }

                foreach (var other in wanted.Where(v => v.Side != side))
                {
                    if (ReadCuts(document, familyName).All(c => c.Side != other.Side))
                    {
                        SaveCut(document, familyName, other);
                    }
                }

                cuts.Commit();
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Could not record the cut of {family}", familyName);
                outcome.Warnings.Add($"The cut of '{familyName}' could not be recorded ({ex.GetType().Name}: {ex.Message}), so other junctions' cuts may be lost if this one is removed.");
            }

            if (cutDirectly && original is not null)
            {
                try
                {
                    using var keep = new Transaction(document, "Junction backup");
                    keep.Start();
                    SaveBackup(document, familyName, original);
                    keep.Commit();
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Could not store a copy of the family {family}", familyName);
                    outcome.Warnings.Add($"A copy of '{familyName}' could not be stored in the project ({ex.GetType().Name}: {ex.Message}), so Remove T-Junction can't restore it.");
                }
            }

            return job is not null || earlier.Count > 0;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not edit the family {family} for the junction", familyName);
            outcome.Warnings.Add($"'{familyName}' could not be cut ({ex.GetType().Name}: {ex.Message}) and was left as it is.");
            return false;
        }
        finally
        {
            familyDocument?.Close(false);
        }
    }

    /// <summary>A void a junction put in a family, as the data to make it again. The points are in the family's coordinates.</summary>
    private sealed class StoredVoid
    {
        public StoredVoid(string side, string role, IReadOnlyList<XYZ> points, double height)
        {
            Side = side;
            Role = role;
            Points = points;
            Height = height;
        }

        public string Side { get; }
        public string Role { get; }
        public IReadOnlyList<XYZ> Points { get; }
        public double Height { get; }
    }

    private static string FormatNumber(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatPoints(IEnumerable<XYZ> points) =>
        string.Join(";", points.Select(p => FormatNumber(p.X) + "," + FormatNumber(p.Y) + "," + FormatNumber(p.Z)));

    private static List<XYZ> ParsePoints(string text)
    {
        var points = new List<XYZ>();
        foreach (var part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var v = part.Split(',').Select(n => double.Parse(n, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            points.Add(new XYZ(v[0], v[1], v[2]));
        }

        return points;
    }

    /// <summary>The voids in a family, and whether one of them (including one made by an earlier version) is this junction's.</summary>
    private static List<StoredVoid> ReadVoids(Document familyDocument, string side, out bool hadOwn, out int unknownOthers)
    {
        var schema = VoidDataSchema();
        var legacy = VoidSchema();
        var voids = new List<StoredVoid>();
        hadOwn = false;
        unknownOthers = 0;
        foreach (var extrusion in new FilteredElementCollector(familyDocument).OfClass(typeof(Extrusion)))
        {
            if (extrusion.GetEntity(schema) is { } entity && entity.IsValid())
            {
                var found = new StoredVoid(entity.Get<string>(SideField), entity.Get<string>(RoleField), ParsePoints(entity.Get<string>(PointsField)),
                    double.Parse(entity.Get<string>(HeightField), System.Globalization.CultureInfo.InvariantCulture));
                voids.Add(found);
                hadOwn |= found.Side == side;
            }
            else if (extrusion.GetEntity(legacy) is { } old && old.IsValid())
            {
                // a void from an earlier version: it holds no data to cut it again from
                if (old.Get<string>(SideField) == side)
                {
                    hadOwn = true;
                }
                else
                {
                    unknownOthers++;
                }
            }
        }

        return voids;
    }

    /// <summary>What one family needs to be cut, read from the road part while its element is still valid.</summary>
    private sealed class VoidJob
    {
        public VoidJob(string familyId, IReadOnlyList<XYZ> polygon, string role, double minZ, double maxZ, Transform toFamily)
        {
            FamilyName = familyId;
            Polygon = polygon;
            Role = role;
            MinZ = minZ;
            MaxZ = maxZ;
            ToFamily = toFamily;
        }

        public string FamilyName { get; }
        public IReadOnlyList<XYZ> Polygon { get; }
        public string Role { get; }
        public double MinZ { get; }
        public double MaxZ { get; }

        /// <summary>Takes a point in the model into the family's own coordinates (the family is built around the instance's origin).</summary>
        public Transform ToFamily { get; }
    }

    private static VoidJob? MakeJob(FamilyInstance part, IReadOnlyList<XYZ> polygon, string role)
    {
        if (part.Symbol?.Family is not { } family || part.get_BoundingBox(null) is not { } box)
        {
            return null;
        }

        // only the parts the junction actually reaches get a void
        if (!Reaches(part, polygon, box.Min.Z - Margin, box.Max.Z - box.Min.Z + 2.0 * Margin))
        {
            return null;
        }

        return new VoidJob(family.Name, polygon, role, box.Min.Z, box.Max.Z, part.GetTransform().Inverse);
    }

    /// <summary>The family file as a base64 string, taken before the junction changes it.</summary>
    private static string? CaptureFamily(Document familyDocument, string familyName)
    {
        var folder = Path.Combine(Path.GetTempPath(), "TopoAlign-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, familyName + ".rfa");
            familyDocument.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
            return Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not keep a copy of the family {family}", familyName);
            return null;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* temp files */ }
        }
    }

    private static bool LoadFamilyFile(Document document, string familyName, byte[] bytes, CutOutcome outcome)
    {
        var folder = Path.Combine(Path.GetTempPath(), "TopoAlign-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, familyName + ".rfa");
            File.WriteAllBytes(path, bytes);
            if (bytes.Length < 8 || bytes[0] != 0xD0 || bytes[1] != 0xCF || bytes[2] != 0x11 || bytes[3] != 0xE0)
            {
                outcome.Warnings.Add($"The stored copy of '{familyName}' is not a valid family file ({bytes.Length} bytes), so it was not restored.");
                return false;
            }

            // Opened as a family document and loaded from there, the same way the junction's own edit is loaded, so Revit's own
            // errors come through if the file is bad.
            var restored = document.Application.OpenDocumentFile(path);
            try
            {
                if (!restored.IsFamilyDocument)
                {
                    outcome.Warnings.Add($"The stored copy of '{familyName}' did not open as a family, so it was not restored.");
                    return false;
                }

                restored.LoadFamily(document, new FamilyOverwriteOptions());
            }
            finally
            {
                restored.Close(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not put the family {family} back", familyName);
            outcome.Warnings.Add($"'{familyName}' could not be put back as it was ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { /* temp files */ }
        }
    }

    /// <summary>
    /// True if the part's geometry overlaps the polygon (taken up through <paramref name="height"/> from <paramref name="z0"/>).
    /// If that can't be worked out, the part is taken to overlap, so it is cut rather than missed.
    /// </summary>
    private static bool Reaches(FamilyInstance part, IReadOnlyList<XYZ> polygon, double z0, double height)
    {
        try
        {
            var loop = new CurveLoop();
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = new XYZ(polygon[i].X, polygon[i].Y, z0);
                var b = new XYZ(polygon[(i + 1) % polygon.Count].X, polygon[(i + 1) % polygon.Count].Y, z0);
                loop.Append(Line.CreateBound(a, b));
            }

            var tool = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, height);

            var solids = new List<Solid>();
            foreach (var item in part.get_Geometry(new Options()))
            {
                switch (item)
                {
                    case Solid solid when solid.Volume > 1e-9:
                        solids.Add(solid);
                        break;
                    case GeometryInstance instance:
                        solids.AddRange(instance.GetInstanceGeometry().OfType<Solid>().Where(s => s.Volume > 1e-9));
                        break;
                }
            }

            if (solids.Count == 0)
            {
                return true;
            }

            foreach (var solid in solids)
            {
                try
                {
                    var common = BooleanOperationsUtils.ExecuteBooleanOperation(solid, tool, BooleanOperationsType.Intersect);
                    if (common is { Volume: > 1e-6 })
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    return true; // the boolean failed, so the part may well overlap
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            log.Information(ex, "Could not tell whether {part} reaches the junction", part.Name);
            return true;
        }
    }

    /// <summary>Adds the void and returns true if any solid had to be cut directly rather than by the void.</summary>
    private static bool AddVoid(Document familyDocument, Schema schema, VoidJob job, string side, CutOutcome outcome)
    {
        var directCuts = false;
        var role = job.Role;
        var polygon = job.Polygon;
        var z0 = job.MinZ - Margin;
        var height = job.MaxZ - job.MinZ + 2.0 * Margin;

        var points = polygon.Select(p => job.ToFamily.OfPoint(new XYZ(p.X, p.Y, z0))).ToList();
        var level = points[0].Z;

        var profile = new CurveArray();
        for (var i = 0; i < points.Count; i++)
        {
            profile.Append(Line.CreateBound(points[i], points[(i + 1) % points.Count]));
        }

        var profiles = new CurveArrArray();
        profiles.Append(profile);

        // solids made by an earlier void in this same edit have no geometry until the family is regenerated
        familyDocument.Regenerate();
        var solids = new FilteredElementCollector(familyDocument).OfClass(typeof(FreeFormElement)).Cast<FreeFormElement>().Where(f => f.IsSolid).ToList();
        var before = solids.ToDictionary(f => f.Id, Volume);

        var sketchPlane = SketchPlane.Create(familyDocument, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0.0, 0.0, level)));
        var voidForm = familyDocument.FamilyCreate.NewExtrusion(false, profiles, sketchPlane, height); // false: a void

        var entity = new Entity(schema);
        entity.Set(SideField, side);
        entity.Set(RoleField, role);
        entity.Set(PointsField, FormatPoints(points));
        entity.Set(HeightField, FormatNumber(height));
        voidForm.SetEntity(entity);

        // the void's geometry has to exist before it can cut anything
        familyDocument.Regenerate();

        // A void in a family may already cut the solids it overlaps, so that is checked first. The API can't set a cut explicitly
        // in a family (SolidSolidCutUtils only works in projects), so otherwise the solid itself is cut.
        var cut = 0;
        foreach (var solid in solids)
        {
            if (Volume(solid) < before[solid.Id] - 1e-6)
            {
                cut++;
                continue;
            }

            // Fallback: cut the solid's geometry itself and rebuild it. The cut is then part of the solid, so deleting the void won't put it back.
            try
            {
                if (CutSolidDirectly(familyDocument, solid, points, height))
                {
                    cut++;
                    directCuts = true;
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "The {role} void could not cut a solid directly", role);
                outcome.Warnings.Add($"The {role} void could not cut a solid in '{familyDocument.Title}' ({ex.GetType().Name}: {ex.Message}).");
            }
        }

        if (solids.Count == 0)
        {
            outcome.Warnings.Add($"'{familyDocument.Title}' has no solid geometry for the {role} void to cut.");
        }

        if (cut > 0)
        {
            outcome.VoidCuts++;
        }

        return directCuts;
    }

    private static double Volume(FreeFormElement form)
    {
        return form.get_Geometry(new Options())?.OfType<Solid>().Sum(solid => solid.Volume) ?? 0.0;
    }

    private static bool CutSolidDirectly(Document familyDocument, FreeFormElement form, IReadOnlyList<XYZ> points, double height)
    {
        var loop = new CurveLoop();
        for (var i = 0; i < points.Count; i++)
        {
            loop.Append(Line.CreateBound(points[i], points[(i + 1) % points.Count]));
        }

        var tool = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, height);
        return FamilySolidCutter.Cut(familyDocument, form, tool);
    }

    /// <summary>
    /// The plan area beyond the join line that holds side road geometry: the width of the road about the line, and as far past the line
    /// as the road ran (<see cref="SideJoin.Reach"/>) plus a pad. It is deliberately not sized from the parts' bounding boxes: a road that
    /// bends back would then be cut at its far end too. Null if the road did not run past the line; a part that stops short of it is reported.
    /// </summary>
    private static List<XYZ>? SideVoidPolygon(IReadOnlyList<FamilyInstance> parts, SideJoin join, CutOutcome outcome)
    {
        var dx = join.Right.X - join.Left.X;
        var dy = join.Right.Y - join.Left.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
        {
            return null;
        }

        var wx = dx / length;
        var wy = dy / length;

        foreach (var part in parts)
        {
            if (part.get_BoundingBox(null) is not { } box)
            {
                continue;
            }

            var partFarthest = double.MinValue;
            foreach (var x in new[] { box.Min.X, box.Max.X })
            {
                foreach (var y in new[] { box.Min.Y, box.Max.Y })
                {
                    partFarthest = Math.Max(partFarthest, join.SignedDistance(new PlanPoint(x, y)));
                }
            }

            if (partFarthest < -0.05)
            {
                outcome.Warnings.Add($"'{part.Name}' stops short of the junction. Make the road again from its alignment to extend it.");
            }
        }

        if (join.Reach <= 0.0)
        {
            return null;
        }

        var depth = join.Reach + SideDepthPad;
        XYZ At(double lateral, double along) => new(
            join.Left.X + wx * lateral + join.Normal.X * along,
            join.Left.Y + wy * lateral + join.Normal.Y * along,
            0.0);

        return new List<XYZ>
        {
            At(-SideWidthPad, 0.0),
            At(length + SideWidthPad, 0.0),
            At(length + SideWidthPad, depth),
            At(-SideWidthPad, depth)
        };
    }

    // ---- copies of families that had to be cut directly ----------------------------------------------------------

    private static readonly Guid BackupSchemaId = new("c4e29b07-81d3-4a56-b7f0-3a9d6e5c2b18");
    private const string FamilyField = "Family";
    private const string DataField = "Data";

    private static Schema BackupSchema() => Schema.Lookup(BackupSchemaId) ?? BuildSchema(BackupSchemaId, "TopoAlignJunctionBackup", SideField, FamilyField, DataField);

    private static List<DataStorage> Backups(Document document, string familyId)
    {
        var schema = BackupSchema();
        return new FilteredElementCollector(document)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .Where(s => s.GetEntity(schema) is { } entity && entity.IsValid() && entity.Get<string>(FamilyField) == familyId)
            .ToList();
    }

    /// <summary>The copy of a family from before any junction cut it. There is one per family, whichever junctions have cut it.</summary>
    private static string? ReadBackup(Document document, string familyId)
    {
        return Backups(document, familyId).Select(b => b.GetEntity(BackupSchema()).Get<string>(DataField)).FirstOrDefault(d => !string.IsNullOrEmpty(d));
    }

    private static void DeleteBackups(Document document, string familyId)
    {
        var ids = Backups(document, familyId).Select(b => b.Id).ToList();
        if (ids.Count > 0)
        {
            document.Delete(ids);
        }
    }

    private static void SaveBackup(Document document, string familyId, string data)
    {
        DeleteBackups(document, familyId);
        var entity = new Entity(BackupSchema());
        entity.Set(SideField, string.Empty);
        entity.Set(FamilyField, familyId);
        entity.Set(DataField, data);
        DataStorage.Create(document).SetEntity(entity);
    }

    // ---- what each junction cut in each family ---------------------------------------------------------------------

    private static readonly Guid CutSchemaId = new("2b6f9e04-5d17-4a83-bc20-7e41a9d3c658");

    private static Schema CutSchema() => Schema.Lookup(CutSchemaId)
        ?? BuildSchema(CutSchemaId, "TopoAlignJunctionCut", SideField, FamilyField, RoleField, PointsField, HeightField);

    private static List<DataStorage> CutStorages(Document document, string familyId)
    {
        var schema = CutSchema();
        return new FilteredElementCollector(document)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .Where(s => s.GetEntity(schema) is { } entity && entity.IsValid() && entity.Get<string>(FamilyField) == familyId)
            .ToList();
    }

    private static List<StoredVoid> ReadCuts(Document document, string familyId)
    {
        var schema = CutSchema();
        return CutStorages(document, familyId)
            .Select(s => s.GetEntity(schema))
            .Select(e => new StoredVoid(e.Get<string>(SideField), e.Get<string>(RoleField), ParsePoints(e.Get<string>(PointsField)),
                double.Parse(e.Get<string>(HeightField), System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static void DeleteCut(Document document, string familyId, string side)
    {
        var schema = CutSchema();
        var ids = CutStorages(document, familyId).Where(s => s.GetEntity(schema).Get<string>(SideField) == side).Select(s => s.Id).ToList();
        if (ids.Count > 0)
        {
            document.Delete(ids);
        }
    }

    private static void SaveCut(Document document, string familyId, StoredVoid cut)
    {
        var entity = new Entity(CutSchema());
        entity.Set(SideField, cut.Side);
        entity.Set(FamilyField, familyId);
        entity.Set(RoleField, cut.Role);
        entity.Set(PointsField, FormatPoints(cut.Points));
        entity.Set(HeightField, FormatNumber(cut.Height));
        DataStorage.Create(document).SetEntity(entity);
    }

    // ---- the record of which families hold a junction's voids ---------------------------------------------------

    /// <summary>Remembers which families hold the junction's voids, replacing the earlier record. Must be called inside a transaction.</summary>
    public static void SaveRecord(Document document, string junctionKey, IEnumerable<string> families)
    {
        DeleteRecords(document, junctionKey);

        var entity = new Entity(RecordSchema());
        entity.Set(SideField, junctionKey);
        entity.Set(FamiliesField, string.Join("|", families));

        var storage = DataStorage.Create(document);
        storage.SetEntity(entity);
    }

    /// <summary>Deletes the record of the junction's voids. Must be called inside a transaction. Returns how many were deleted.</summary>
    public static int DeleteRecords(Document document, string junctionKey)
    {
        var records = Records(document, junctionKey).Select(r => r.Id).ToList();
        if (records.Count > 0)
        {
            document.Delete(records);
        }

        return records.Count;
    }

    /// <summary>
    /// A family is found by name: loading a family over the one in the project can give it a new element id, so ids don't last.
    /// </summary>
    private static Family? FindFamily(Document document, string name)
    {
        return new FilteredElementCollector(document).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(f => f.Name == name);
    }

    private static List<string> ReadRecord(Document document, string side)
    {
        var ids = new List<string>();
        var schema = RecordSchema();
        foreach (var record in Records(document, side))
        {
            var text = record.GetEntity(schema).Get<string>(FamiliesField);
            ids.AddRange(text.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        }

        return ids;
    }

    private static List<DataStorage> Records(Document document, string side)
    {
        var schema = RecordSchema();
        return new FilteredElementCollector(document)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .Where(s => s.GetEntity(schema) is { } entity && entity.IsValid() && entity.Get<string>(SideField) == side)
            .ToList();
    }
}
