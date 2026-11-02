using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Serilog;
using System.IO;

namespace TopoAlign.Sweep;

/// <summary>Puts road solids into a loadable family, so the road is a family with a type in the project rather than a DirectShape.</summary>
internal static class RoadFamilyBuilder
{
    private static readonly ILogger log = Log.ForContext(typeof(RoadFamilyBuilder));

    private const string NamePrefix = "TopoAlign Road - ";

    /// <summary>Characters Revit does not allow in a family name or that a file name can't hold.</summary>
    private static readonly char[] InvalidNameCharacters = Path.GetInvalidFileNameChars()
        .Concat(new[] { '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~', ':', '\\' })
        .Distinct()
        .ToArray();

    /// <summary>The family name for an alignment's road. The id on the end keeps two alignments with the same name apart and survives renaming.</summary>
    public static string FamilyName(Element alignment, string? alignmentName)
    {
        var cleaned = new string((alignmentName is null || string.IsNullOrWhiteSpace(alignmentName) ? "Alignment" : alignmentName.Trim())
            .Select(c => InvalidNameCharacters.Contains(c) ? '_' : c)
            .ToArray());
        return $"{NamePrefix}{cleaned} ({IdOf(alignment)})";
    }

    /// <summary>True for a road family this add-in made, whichever alignment it is for.</summary>
    public static bool IsRoadFamily(Family? family)
    {
        return family is not null && family.Name.StartsWith(NamePrefix, StringComparison.Ordinal);
    }

    /// <summary>The road families this add-in has made for an alignment.</summary>
    public static List<Family> FindFamilies(Document document, Element alignment, string? alignmentName)
    {
        var suffix = $" ({IdOf(alignment)})";
        return new FilteredElementCollector(document)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(family => family.Name.StartsWith(NamePrefix, StringComparison.Ordinal) && family.Name.EndsWith(suffix, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Makes a family file holding the solids as free-form geometry and returns its path. Opens (and closes) a family
    /// document; the project is not touched. Delete the file with <see cref="DeleteFile"/> when done.
    /// </summary>
    public static string CreateFamilyFile(Document document, string familyName, IReadOnlyList<Solid> solids, ElementId categoryId)
    {
        var template = FindTemplate(document.Application)
                       ?? throw new InvalidOperationException(
                           "No Generic Model family template was found, so the road can't be made as a family. Looked in: "
                           + string.Join("; ", TemplateFolders(document.Application)));

        var folder = Path.Combine(Path.GetTempPath(), "TopoAlign", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, familyName + ".rfa");

        var familyDocument = document.Application.NewFamilyDocument(template);
        try
        {
            using (var transaction = new Transaction(familyDocument, "Road geometry"))
            {
                transaction.Start();

                try
                {
                    var category = Category.GetCategory(familyDocument, categoryId);
                    if (category is not null && familyDocument.OwnerFamily.FamilyCategory?.Id != category.Id)
                    {
                        familyDocument.OwnerFamily.FamilyCategory = category;
                    }
                }
                catch (Exception ex)
                {
                    // not every category can hold a family; the template's own (Generic Models) stays
                    log.Information(ex, "Road family kept the template category");
                }

                // The geometry is built around the family's origin and the instance is placed at FamilyOrigin, so it
                // lands exactly where the solids were built and nothing sits thousands of feet from the family origin.
                var toFamily = Transform.CreateTranslation(-FamilyOrigin(solids));
                foreach (var solid in solids)
                {
                    FreeFormElement.Create(familyDocument, SolidUtils.CreateTransformed(solid, toFamily));
                }

                transaction.Commit();
            }

            familyDocument.SaveAs(file, new SaveAsOptions { OverwriteExistingFile = true });
        }
        catch (Exception)
        {
            DeleteFile(file);
            throw;
        }
        finally
        {
            familyDocument.Close(false);
        }

        return file;
    }

    /// <summary>
    /// Loads the family file into the project and places one instance of it at <see cref="FamilyOrigin"/>, the point the
    /// family geometry was built around. Call inside a transaction.
    /// </summary>
    public static (Family Family, FamilyInstance Instance) Place(Document document, string file, IReadOnlyList<Solid> solids)
    {
        if (!document.LoadFamily(file, new OverwriteOptions(), out var family) || family is null)
        {
            throw new InvalidOperationException("Revit could not load the road family.");
        }

        var symbolId = family.GetFamilySymbolIds().FirstOrDefault()
                       ?? throw new InvalidOperationException("The road family has no type.");
        var symbol = (FamilySymbol)document.GetElement(symbolId);
        if (!symbol.IsActive)
        {
            symbol.Activate();
            document.Regenerate();
        }

        var level = new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
        var origin = FamilyOrigin(solids);
        var instance = level is null
            ? document.Create.NewFamilyInstance(origin, symbol, StructuralType.NonStructural)
            : document.Create.NewFamilyInstance(origin, symbol, level, StructuralType.NonStructural);

        document.Regenerate();
        CheckPosition(instance, solids);
        return (family, instance);
    }

    /// <summary>
    /// The point the family geometry is built around: the middle of the road in plan, at its lowest level.
    /// Computed from the solids, so creating the family and placing it agree without passing anything between them.
    /// </summary>
    internal static XYZ FamilyOrigin(IReadOnlyList<Solid> solids)
    {
        if (!TryGetBounds(solids, out var min, out var max))
        {
            return XYZ.Zero;
        }

        return new XYZ((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, min.Z);
    }

    /// <summary>The box round the solids in model coordinates, from all eight corners of each solid's own box.</summary>
    private static bool TryGetBounds(IReadOnlyList<Solid> solids, out XYZ min, out XYZ max)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        var any = false;

        foreach (var solid in solids)
        {
            var box = solid?.GetBoundingBox();
            if (box is null)
            {
                continue;
            }

            foreach (var x in new[] { box.Min.X, box.Max.X })
            {
                foreach (var y in new[] { box.Min.Y, box.Max.Y })
                {
                    foreach (var z in new[] { box.Min.Z, box.Max.Z })
                    {
                        var point = box.Transform.OfPoint(new XYZ(x, y, z));
                        minX = Math.Min(minX, point.X);
                        minY = Math.Min(minY, point.Y);
                        minZ = Math.Min(minZ, point.Z);
                        maxX = Math.Max(maxX, point.X);
                        maxY = Math.Max(maxY, point.Y);
                        maxZ = Math.Max(maxZ, point.Z);
                        any = true;
                    }
                }
            }
        }

        min = any ? new XYZ(minX, minY, minZ) : XYZ.Zero;
        max = any ? new XYZ(maxX, maxY, maxZ) : XYZ.Zero;
        return any;
    }

    /// <summary>
    /// Logs when the instance's box is not where the solids' box is. The two boxes are worked out differently, so small
    /// differences are normal and nothing is moved; this is a diagnostic for a family that has gone astray.
    /// </summary>
    private static void CheckPosition(FamilyInstance instance, IReadOnlyList<Solid> solids)
    {
        var box = instance.get_BoundingBox(null);
        if (box is null || !TryGetBounds(solids, out var min, out _))
        {
            return;
        }

        var offset = min - box.Min;
        if (offset.GetLength() > 0.5)
        {
            log.Warning("Road family instance {id} sits {offset} ft from where its geometry was built", IdOf(instance), offset.GetLength());
        }
    }

    /// <summary>Removes the family file and the temporary folder made for it. Never throws.</summary>
    public static void DeleteFile(string? file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        try
        {
            var folder = Path.GetDirectoryName(file);
            if (File.Exists(file))
            {
                File.Delete(file);
            }

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception ex)
        {
            log.Information(ex, "Could not remove the temporary road family file");
        }
    }

    /// <summary>A template to use instead of looking in the family template folder. For tests on machines without the template installed.</summary>
    internal static string? TemplateOverride { get; set; }

    /// <summary>The Generic Model template installed with this Revit, or null if there isn't one.</summary>
    internal static string? FindTemplate(Autodesk.Revit.ApplicationServices.Application application)
    {
        if (!string.IsNullOrEmpty(TemplateOverride) && File.Exists(TemplateOverride))
        {
            return TemplateOverride;
        }

        // Revit's own template folder first, then the shared Autodesk one for this version
        // (the configured path can point at a folder that only holds a few templates).
        foreach (var folder in TemplateFolders(application).Where(f => Directory.Exists(f)))
        {
            var found = FindTemplateIn(folder);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Where to look for the template: Revit's configured path, the shared folder for this version, then the folders of
    /// older installed versions (Revit upgrades an older template when it opens it). Some installs of a new version
    /// have no Generic Model template at all.
    /// </summary>
    internal static List<string> TemplateFolders(Autodesk.Revit.ApplicationServices.Application application)
    {
        var folders = new List<string>();
        if (!string.IsNullOrEmpty(application.FamilyTemplatePath))
        {
            folders.Add(application.FamilyTemplatePath);
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk");
        if (!int.TryParse(application.VersionNumber, out var current))
        {
            return folders;
        }

        for (var year = current; year >= current - 6; year--)
        {
            var folder = Path.Combine(root, "RVT " + year, "Family Templates");
            if (!folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }

    /// <summary>
    /// The plain Generic Model template in a folder. Metric installs call it "Metric Generic Model.rft"
    /// and imperial ones "Generic Model.rft"; the face, wall, line-based and similar variants are not wanted.
    /// </summary>
    internal static string? FindTemplateIn(string folder)
    {
        var templates = Directory.EnumerateFiles(folder, "*Generic Model*.rft", SearchOption.AllDirectories).ToList();
        return templates.FirstOrDefault(t => IsPlainName(t, "Metric Generic Model"))
               ?? templates.FirstOrDefault(t => IsPlainName(t, "Generic Model"));
    }

    private static bool IsPlainName(string path, string name)
    {
        return string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase);
    }

    private static string IdOf(Element element)
    {
#if REVIT2024_OR_GREATER
        return element.Id.Value.ToString();
#else
        return element.Id.IntegerValue.ToString();
#endif
    }

    /// <summary>Replaces a family of the same name instead of asking.</summary>
    private sealed class OverwriteOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
