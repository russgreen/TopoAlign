using Autodesk.Revit.DB;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TopoAlign.Alignments;

/// <summary>
/// Reflection wrapper over Autodesk's Civil Alignments add-in
/// (AddIns\CivilAlignments\Autodesk.CivilAlignments.DBApplication.dll).
/// Revit's public API has no Alignment class, so everything goes through this
/// one adapter. Create/update use non-public members and may change between Revit versions.
/// </summary>
internal static class AlignmentApi
{
    private const string AssemblyFileName = "Autodesk.CivilAlignments.DBApplication.dll";
    private const string AssemblyName = "Autodesk.CivilAlignments.DBApplication";
    private const string NamespacePrefix = "Autodesk.Revit.DB.Infrastructure.";

    private static readonly ILogger log = Log.ForContext(typeof(AlignmentApi));
    private const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static bool _initialised;
    private static Type _alignmentType;
    private static Type _schemaType;
    private static Type _alignmentDataType;

    public static bool IsAvailable
    {
        get
        {
            Initialise();
            return _alignmentType is not null;
        }
    }

    private static void Initialise()
    {
        if (_initialised)
        {
            return;
        }

        _initialised = true;

        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase));

            if (assembly is null)
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AddIns", "CivilAlignments", AssemblyFileName);
                if (!File.Exists(path))
                {
                    log.Warning("Civil Alignments assembly not found at {path}", path);
                    return;
                }

                assembly = Assembly.LoadFrom(path);
            }

            _alignmentType = assembly.GetType(NamespacePrefix + "Alignment");
            _schemaType = assembly.GetType(NamespacePrefix + "AlignmentSchema");
            _alignmentDataType = assembly.GetType(NamespacePrefix + "AlignmentData");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load Civil Alignments assembly");
            _alignmentType = null;
            _schemaType = null;
            _alignmentDataType = null;
        }
    }

    /// <summary>True if the element is a valid native alignment.</summary>
    public static bool IsValid(Element element)
    {
        if (element is null || !IsAvailable)
        {
            return false;
        }

        try
        {
            var method = _alignmentType.GetMethod("IsValid", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Element) }, null);
            return method is not null && (bool)method.Invoke(null, new object[] { element });
        }
        catch (Exception ex)
        {
            log.Debug(ex, "IsValid failed");
            return false;
        }
    }

    /// <summary>All native alignment elements in the document.</summary>
    public static IList<Element> GetAlignmentElements(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Alignments)
            .WhereElementIsNotElementType()
            .Where(IsValid)
            .ToList();
    }

    /// <summary>Every element in the Alignments category, valid or not (diagnostics).</summary>
    public static IList<Element> GetAlignmentCategoryElements(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Alignments)
            .WhereElementIsNotElementType()
            .ToList();
    }

    /// <summary>Basic read-only info for an alignment element, or null.</summary>
    public static AlignmentInfo GetInfo(Element element)
    {
        if (!IsValid(element))
        {
            return null;
        }

        try
        {
            var get = _alignmentType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Element) }, null);
            var alignment = get?.Invoke(null, new object[] { element });
            if (alignment is null)
            {
                return null;
            }

            return new AlignmentInfo
            {
                ElementId = element.Id,
                Name = ReadProperty<string>(alignment, "Name"),
                Description = ReadProperty<string>(alignment, "Description"),
                StartStation = ReadProperty<double>(alignment, "StartStation"),
                EndStation = ReadProperty<double>(alignment, "EndStation"),
                DisplayedStartStation = ReadProperty<double>(alignment, "DisplayedStartStation"),
                DisplayedEndStation = ReadProperty<double>(alignment, "DisplayedEndStation"),
            };
        }
        catch (Exception ex)
        {
            log.Error(ex, "GetInfo failed");
            return null;
        }
    }

    /// <summary>XYZ on the alignment at a station (internal units), or null.</summary>
    public static XYZ GetPointAtStation(Element element, double station)
    {
        if (!IsValid(element))
        {
            return null;
        }

        try
        {
            var get = _alignmentType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Element) }, null);
            var alignment = get?.Invoke(null, new object[] { element });
            var method = _alignmentType.GetMethod("GetPointAtStation", BindingFlags.Public | BindingFlags.Instance);
            return method?.Invoke(alignment, new object[] { station }) as XYZ;
        }
        catch (Exception ex)
        {
            log.Debug(ex, "GetPointAtStation failed");
            return null;
        }
    }

    /// <summary>
    /// Raw Autodesk "infra" alignment JSON stored on the element (AlignmentSchema.GetJSON, internal).
    /// Used to learn the format and as the base for edits.
    /// </summary>
    public static string GetJson(Element element)
    {
        if (element is null || !IsAvailable || _schemaType is null)
        {
            return null;
        }

        try
        {
            var method = _schemaType.GetMethod("GetJSON", AnyMember, null, new[] { typeof(Element) }, null);
            if (method is null)
            {
                log.Warning("AlignmentSchema.GetJSON not found");
                return null;
            }

            var target = method.IsStatic ? null : Activator.CreateInstance(_schemaType, true);
            return method.Invoke(target, new object[] { element }) as string;
        }
        catch (Exception ex)
        {
            log.Error(ex, "GetJson failed");
            return null;
        }
    }

    public static ElementId Create(Document doc, string name, string json, double displayedStartDistance, ICollection<ElementId> sourceCurveIdsToDelete = null)
    {
        if (doc is null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        if (!IsAvailable || _alignmentDataType is null)
        {
            throw new InvalidOperationException("Civil Alignments API is not available.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Alignment JSON is required.", nameof(json));
        }

        var createMethod = _alignmentType.GetMethod("CreateAlignment", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Document), _alignmentDataType }, null);
        if (createMethod is null)
        {
            throw new MissingMethodException("Alignment.CreateAlignment(Document, AlignmentData) was not found.");
        }

        var alignmentData = Activator.CreateInstance(_alignmentDataType, true);
        _alignmentDataType.GetProperty("JSON", AnyMember)?.SetValue(alignmentData, json);
        _alignmentDataType.GetProperty("DisplayedStartDistance", AnyMember)?.SetValue(alignmentData, displayedStartDistance);

        var fh = new FailureHandler();

        using var transaction = new Transaction(doc, "Create alignment");
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(fh);
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        var alignment = createMethod.Invoke(null, new[] { doc, alignmentData });
        if (alignment is null)
        {
            transaction.RollBack();
            throw new InvalidOperationException("Civil Alignments API returned no alignment object.");
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            alignment.GetType().GetProperty("Name", AnyMember)?.SetValue(alignment, name);
        }

        var alignmentElement = alignment.GetType().GetProperty("Element", AnyMember)?.GetValue(alignment) as Element;

        if (sourceCurveIdsToDelete is not null && sourceCurveIdsToDelete.Count > 0)
        {
            doc.Delete(sourceCurveIdsToDelete.Where(id => id is not null && id != ElementId.InvalidElementId).ToList());
        }

        transaction.Commit();

        return alignmentElement?.Id ?? ElementId.InvalidElementId;
    }

    private static T ReadProperty<T>(object instance, string name)
    {
        var value = instance.GetType().GetProperty(name, AnyMember)?.GetValue(instance);
        return value is T typed ? typed : default;
    }
}

internal class AlignmentInfo
{
    public ElementId ElementId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double DisplayedStartStation { get; set; }
    public double DisplayedEndStation { get; set; }
}
