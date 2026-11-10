using Autodesk.Revit.DB;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TopoAlign.Alignments.Civil;

/// <summary>
/// Binds, once, to the types and members of Autodesk's Civil Alignments add-in
/// (AddIns\CivilAlignments\Autodesk.CivilAlignments.DBApplication.dll). Revit's public API has no Alignment class, so
/// this is the only place that knows the names of Autodesk's types, methods and properties; <see cref="AlignmentApi"/>
/// builds its behaviour on the <see cref="MethodInfo"/>s and <see cref="PropertyInfo"/>s found here.
/// The names and signatures are the ones declared by Autodesk's own DLL, and <see cref="AlignmentApi.Supports"/> checks at
/// run time that the installed version still has the ones a command needs. Anything that wasn't found is null.
/// </summary>
internal static class CivilAlignmentsBinding
{
    private const string AssemblyFileName = "Autodesk.CivilAlignments.DBApplication.dll";
    private const string AssemblyName = "Autodesk.CivilAlignments.DBApplication";
    private const string NamespacePrefix = "Autodesk.Revit.DB.Infrastructure.";

    /// <summary>For members that are non-public in Autodesk's DLL, or whose visibility has varied between versions.</summary>
    internal const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static readonly ILogger log = Log.ForContext(typeof(CivilAlignmentsBinding));

    // The static initialiser runs once, on first use, and never throws: when the add-in can't be bound everything is null.
    private static readonly ResolvedCivilMembers? resolved = Resolve();

    /// <summary>True if the Civil Alignments add-in was found and its alignment type could be bound.</summary>
    public static bool IsAvailable => resolved?.Alignment is not null;

    // ---- types ----------------------------------------------------------------------------------------------------

    public static Type? AlignmentType => resolved?.Alignment;
    public static Type? AlignmentDataType => resolved?.AlignmentData;
    public static Type? LabelType => resolved?.Label;
    public static Type? LabelSetOptionsType => resolved?.LabelSetOptions;

    // ---- Alignment ------------------------------------------------------------------------------------------------

    /// <summary><c>Alignment.Get(Element)</c>: the alignment object for an alignment element.</summary>
    public static MethodInfo? Get => resolved?.Get;

    /// <summary><c>Alignment.IsValid(Element)</c>.</summary>
    public static MethodInfo? IsValid => resolved?.IsValid;

    /// <summary><c>Alignment.CreateAlignment(Document, AlignmentData)</c>.</summary>
    public static MethodInfo? CreateAlignment => resolved?.CreateAlignment;

    /// <summary><c>Alignment.UpdateAlignment(Document, AlignmentData)</c>.</summary>
    public static MethodInfo? UpdateAlignment => resolved?.UpdateAlignment;

    public static MethodInfo? GetPointAtStation => resolved?.GetPointAtStation;
    public static MethodInfo? GetHCurveTangentAtStation => resolved?.GetHCurveTangentAtStation;
    public static MethodInfo? GetDisplayedHorizontalCurveEndpoints => resolved?.GetDisplayedHorizontalCurveEndpoints;

    // ---- AlignmentSchema, AlignmentData, Utilities ---------------------------------------------------------------

    /// <summary><c>AlignmentSchema.GetJSON(Element)</c>: the infra JSON stored on an alignment element.</summary>
    public static MethodInfo? GetJson => resolved?.GetJson;

    /// <summary><c>Utilities.GetAlignmentLength(string)</c>: the length, in internal units, of an infra JSON document.</summary>
    public static MethodInfo? GetAlignmentLength => resolved?.GetAlignmentLength;

    public static PropertyInfo? DataJson => resolved?.DataJson;
    public static PropertyInfo? DataDisplayedStartDistance => resolved?.DataDisplayedStartDistance;
    public static PropertyInfo? DataDisplayedEndDistance => resolved?.DataDisplayedEndDistance;

    // ---- AlignmentStationLabel ------------------------------------------------------------------------------------

    public static MethodInfo? LabelCreateSet => resolved?.LabelCreateSet;
    public static MethodInfo? LabelIsValidType => resolved?.LabelIsValidType;
    public static MethodInfo? LabelIsRecommendedTypeForSet => resolved?.LabelIsRecommendedTypeForSet;
    public static MethodInfo? LabelCreateRecommendedTypeForSet => resolved?.LabelCreateRecommendedTypeForSet;

    private static ResolvedCivilMembers? Resolve()
    {
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
                    return null;
                }

                assembly = Assembly.LoadFrom(path);
            }

            var alignment = assembly.GetType(NamespacePrefix + "Alignment");
            var schema = assembly.GetType(NamespacePrefix + "AlignmentSchema");
            var data = assembly.GetType(NamespacePrefix + "AlignmentData");
            var utilities = assembly.GetType(NamespacePrefix + "Utilities");
            var label = assembly.GetType(NamespacePrefix + "AlignmentStationLabel");
            var labelOptions = assembly.GetType(NamespacePrefix + "AlignmentStationLabelSetOptions");

            var element = new[] { typeof(Element) };
            var document = new[] { typeof(Document) };
            var documentAndData = data is null ? null : new[] { typeof(Document), data };

            return new ResolvedCivilMembers
            {
                Alignment = alignment,
                AlignmentData = data,
                Label = label,
                LabelSetOptions = labelOptions,

                Get = FindMethod(alignment, "Get", PublicStatic, element),
                IsValid = FindMethod(alignment, "IsValid", PublicStatic, element),
                CreateAlignment = documentAndData is null ? null : FindMethod(alignment, "CreateAlignment", AnyMember, documentAndData),
                UpdateAlignment = documentAndData is null ? null : FindMethod(alignment, "UpdateAlignment", AnyMember, documentAndData),
                GetPointAtStation = FindMethod(alignment, "GetPointAtStation", PublicInstance),
                GetHCurveTangentAtStation = FindMethod(alignment, "GetHCurveTangentAtStation", PublicInstance),
                GetDisplayedHorizontalCurveEndpoints = FindMethod(alignment, "GetDisplayedHorizontalCurveEndpoints", PublicInstance),

                GetJson = FindMethod(schema, "GetJSON", AnyMember, element),
                GetAlignmentLength = FindMethod(utilities, "GetAlignmentLength", AnyMember, new[] { typeof(string) }),
                DataJson = data?.GetProperty("JSON", AnyMember),
                DataDisplayedStartDistance = data?.GetProperty("DisplayedStartDistance", AnyMember),
                DataDisplayedEndDistance = data?.GetProperty("DisplayedEndDistance", AnyMember),

                LabelCreateSet = FindMethod(label, "CreateSet", PublicStatic),
                LabelIsValidType = FindMethod(label, "IsValidType", PublicStatic, element),
                LabelIsRecommendedTypeForSet = FindMethod(label, "IsRecommendedTypeForSet", PublicStatic, element),
                LabelCreateRecommendedTypeForSet = FindMethod(label, "CreateRecommendedTypeForSet", PublicStatic, document)
            };
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to load Civil Alignments assembly");
            return null;
        }
    }

    /// <summary>Finds a method by name (and parameter types, when given); null if it isn't there or the name is ambiguous.</summary>
    private static MethodInfo? FindMethod(Type? type, string name, BindingFlags flags, Type[]? parameterTypes = null)
    {
        if (type is null)
        {
            return null;
        }

        try
        {
            return parameterTypes is null
                ? type.GetMethod(name, flags)
                : type.GetMethod(name, flags, null, parameterTypes, null);
        }
        catch (AmbiguousMatchException ex)
        {
            log.Warning(ex, "{type}.{method} is ambiguous", type.Name, name);
            return null;
        }
    }
}
