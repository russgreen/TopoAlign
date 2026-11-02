using Autodesk.Revit.DB;
using Serilog;
using System;
using System.Collections.Generic;
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
    private static readonly ILogger log = Log.ForContext(typeof(AlignmentApi));
    private const BindingFlags AnyMember = CivilAlignmentsBinding.AnyMember;

    public static bool IsAvailable => CivilAlignmentsBinding.IsAvailable;

    /// <summary>The parts of Autodesk's Civil Alignments add-in that TopoAlign's commands rely on.</summary>
    [Flags]
    internal enum AlignmentFeature
    {
        /// <summary>Reading alignments: Alignment.Get, GetPointAtStation and the stored JSON.</summary>
        Read = 1,

        /// <summary>Creating alignments.</summary>
        Create = 2,

        /// <summary>Changing an existing alignment.</summary>
        Update = 4,

        /// <summary>Station label sets.</summary>
        StationLabels = 8,

        All = Read | Create | Update | StationLabels
    }

    /// <summary>
    /// Checks that the installed Civil Alignments add-in has the (partly non-public) members a command needs, so a Revit
    /// version that changed them gives one clear message instead of an obscure failure half way through. The result is logged.
    /// </summary>
    /// <param name="problem">What is missing, ready to show to the user, or null when everything needed is there.</param>
    public static bool Supports(AlignmentFeature features, out string? problem)
    {
        problem = null;
        if (!IsAvailable)
        {
            problem = "The Autodesk Civil Alignments add-in was not found in this Revit installation, so alignments are not available.";
            return false;
        }

        var missing = new List<string>();

        void Require(bool present, string name)
        {
            if (!present)
            {
                missing.Add(name);
            }
        }

        if (features.HasFlag(AlignmentFeature.Read))
        {
            Require(CivilAlignmentsBinding.Get is not null, "Alignment.Get");
            Require(CivilAlignmentsBinding.GetPointAtStation is not null, "Alignment.GetPointAtStation");
            Require(CivilAlignmentsBinding.GetJson is not null, "AlignmentSchema.GetJSON");
        }

        if (features.HasFlag(AlignmentFeature.Create) || features.HasFlag(AlignmentFeature.Update))
        {
            Require(CivilAlignmentsBinding.AlignmentDataType is not null, "AlignmentData");
            Require(CivilAlignmentsBinding.DataJson is not null, "AlignmentData.JSON");
            Require(CivilAlignmentsBinding.DataDisplayedStartDistance is not null, "AlignmentData.DisplayedStartDistance");
            Require(CivilAlignmentsBinding.DataDisplayedEndDistance is not null, "AlignmentData.DisplayedEndDistance");
        }

        if (CivilAlignmentsBinding.AlignmentDataType is not null)
        {
            if (features.HasFlag(AlignmentFeature.Create))
            {
                Require(CivilAlignmentsBinding.CreateAlignment is not null, "Alignment.CreateAlignment");
            }

            if (features.HasFlag(AlignmentFeature.Update))
            {
                Require(CivilAlignmentsBinding.UpdateAlignment is not null, "Alignment.UpdateAlignment");
            }
        }

        if (features.HasFlag(AlignmentFeature.StationLabels))
        {
            Require(CanCreateStationLabels, "AlignmentStationLabel");
            Require(CivilAlignmentsBinding.LabelCreateSet is not null, "AlignmentStationLabel.CreateSet");
            Require(CivilAlignmentsBinding.LabelIsValidType is not null, "AlignmentStationLabel.IsValidType");
        }

        if (missing.Count == 0)
        {
            return true;
        }

        problem = "This version of Revit's Civil Alignments add-in is missing " + string.Join(", ", missing) +
                  ", so this command can't run. TopoAlign may need updating for this version of Revit.";
        log.Warning("Civil Alignments self-check failed for {features}: {missing}", features, missing);
        return false;
    }

    private static Type LabelSetOptionsType => CivilAlignmentsBinding.LabelSetOptionsType
        ?? throw new InvalidOperationException("Civil Alignments station labels are not available.");

    /// <summary>Autodesk's alignment object for an alignment element, or null if it can't be read.</summary>
    private static object? GetAlignment(Element element) =>
        CivilAlignmentsBinding.Get?.Invoke(null, new object[] { element });

    /// <summary>
    /// A new <c>AlignmentData</c> for <see cref="Create"/> and <see cref="Update"/>. The displayed start and end are distances
    /// along the alignment (0 .. length), not stations; the end is Autodesk's own length for the JSON where it agrees with
    /// <paramref name="planLength"/>.
    /// </summary>
    private static object CreateAlignmentData(string json, double planLength)
    {
        var dataType = CivilAlignmentsBinding.AlignmentDataType
                       ?? throw new InvalidOperationException("Civil Alignments API is not available.");

        var alignmentData = Activator.CreateInstance(dataType, true)
                            ?? throw new InvalidOperationException("Could not create the alignment data.");
        CivilAlignmentsBinding.DataJson?.SetValue(alignmentData, json);
        CivilAlignmentsBinding.DataDisplayedStartDistance?.SetValue(alignmentData, 0.0);
        CivilAlignmentsBinding.DataDisplayedEndDistance?.SetValue(alignmentData, ResolveDisplayedEndDistance(json, planLength));
        return alignmentData;
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
            return CivilAlignmentsBinding.IsValid?.Invoke(null, new object[] { element }) is true;
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
    public static AlignmentInfo? GetInfo(Element element)
    {
        if (!IsValid(element))
        {
            return null;
        }

        try
        {
            var alignment = GetAlignment(element);
            if (alignment is null)
            {
                return null;
            }

            return new AlignmentInfo
            {
                ElementId = element.Id,
                Name = ReadProperty<string>(alignment, "Name") ?? string.Empty,
                Description = ReadProperty<string>(alignment, "Description") ?? string.Empty,
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
    public static XYZ? GetPointAtStation(Element element, double station)
    {
        if (!IsValid(element))
        {
            return null;
        }

        try
        {
            return CivilAlignmentsBinding.GetPointAtStation?.Invoke(GetAlignment(element), new object[] { station }) as XYZ;
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
    public static string? GetJson(Element element)
    {
        if (element is null || !IsAvailable)
        {
            return null;
        }

        try
        {
            var method = CivilAlignmentsBinding.GetJson;
            if (method is null)
            {
                log.Warning("AlignmentSchema.GetJSON not found");
                return null;
            }

            var target = method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType!, true);
            return method.Invoke(target, new object[] { element }) as string;
        }
        catch (Exception ex)
        {
            log.Error(ex, "GetJson failed");
            return null;
        }
    }

    /// <summary>
    /// Creates a native alignment from Autodesk "infra" alignment JSON.
    /// The displayed start/end are distances along the alignment (0 .. length), not stations; the start station
    /// lives in the JSON. Must be called from within the Revit API context.
    /// </summary>
    /// <param name="planLength">Alignment length in feet, used if Autodesk's own length calculation is unavailable.</param>
    public static ElementId Create(Document doc, string name, string json, double planLength, ICollection<ElementId>? sourceCurveIdsToDelete = null)
    {
        if (doc is null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        if (!IsAvailable || CivilAlignmentsBinding.AlignmentDataType is null)
        {
            throw new InvalidOperationException("Civil Alignments API is not available.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Alignment JSON is required.", nameof(json));
        }

        // CreateAlignment and the AlignmentData setters are non-public in the Civil Alignments DLL.
        var createMethod = CivilAlignmentsBinding.CreateAlignment
                           ?? throw new MissingMethodException("Alignment.CreateAlignment(Document, AlignmentData) was not found.");

        var fh = new FailureHandler();

        using var transaction = new Transaction(doc, "Create alignment");
        transaction.Start();

        var alignmentData = CreateAlignmentData(json, planLength);

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(fh);
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        object? alignment;
        try
        {
            alignment = createMethod.Invoke(null, new[] { doc, alignmentData });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // surface Autodesk's own message rather than the reflection wrapper
            throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
        }

        if (alignment is null)
        {
            transaction.RollBack();
            throw new InvalidOperationException("Civil Alignments API returned no alignment object.");
        }

        var alignmentElement = alignment.GetType().GetProperty("Element", AnyMember)?.GetValue(alignment) as Element;

        if (!string.IsNullOrWhiteSpace(name))
        {
            ApplyName(doc, alignment, alignmentElement, name);
        }

        if (sourceCurveIdsToDelete is not null && sourceCurveIdsToDelete.Count > 0)
        {
            doc.Delete(sourceCurveIdsToDelete.Where(id => id is not null && id != ElementId.InvalidElementId).ToList());
        }

        transaction.Commit();

        return alignmentElement?.Id ?? ElementId.InvalidElementId;
    }

    /// <summary>
    /// Replaces the stored geometry and properties of an existing alignment with new infra JSON (for example after a rename,
    /// a new start station or a reversal), through Autodesk's non-public <c>Alignment.UpdateAlignment</c>. Autodesk finds the
    /// alignment by the guid inside the JSON, so the guid must be kept. Must be called from within the Revit API context.
    /// </summary>
    /// <param name="alignmentElement">The alignment being updated.</param>
    /// <param name="planLength">Alignment length in feet, used if Autodesk's own length calculation is unavailable.</param>
    /// <param name="name">The alignment's new name, so its type can be renamed to match; null to leave the type alone.</param>
    public static void Update(Document doc, Element alignmentElement, string json, double planLength, string? name = null)
    {
        if (doc is null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        if (alignmentElement is null)
        {
            throw new ArgumentNullException(nameof(alignmentElement));
        }

        if (!IsAvailable || CivilAlignmentsBinding.AlignmentDataType is null)
        {
            throw new InvalidOperationException("Civil Alignments API is not available.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Alignment JSON is required.", nameof(json));
        }

        var updateMethod = CivilAlignmentsBinding.UpdateAlignment
                           ?? throw new MissingMethodException("Alignment.UpdateAlignment(Document, AlignmentData) was not found.");

        using var transaction = new Transaction(doc, "Edit alignment");
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        var alignmentData = CreateAlignmentData(json, planLength);

        try
        {
            updateMethod.Invoke(null, new[] { doc, alignmentData });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                if (doc.GetElement(alignmentElement.GetTypeId()) is ElementType type && type.Name != name)
                {
                    type.Name = name;
                }
            }
            catch (Exception ex)
            {
                // for example another type already uses this name; the JSON name is still stored
                log.Debug(ex, "Could not name the alignment type");
            }
        }

        transaction.Commit();
    }

    // ---- station labels -----------------------------------------------------------------------------------------

    /// <summary>True if the installed Civil Alignments add-in can create station label sets.</summary>
    public static bool CanCreateStationLabels =>
        CivilAlignmentsBinding.LabelType is not null && CivilAlignmentsBinding.LabelSetOptionsType is not null;

    /// <summary>A spot-dimension type that can be used for alignment station labels.</summary>
    internal sealed class StationLabelType
    {
        public StationLabelType(ElementId id, string name, bool isRecommended)
        {
            Id = id;
            Name = name;
            IsRecommended = isRecommended;
        }

        public ElementId Id { get; }
        public string Name { get; }

        /// <summary>Autodesk's recommended kind for label sets: rotates with the alignment, vertical text, shows the station, heavy tick leader.</summary>
        public bool IsRecommended { get; }
    }

    /// <summary>The label types in the document, with the ones Autodesk recommends for label sets first.</summary>
    public static IList<StationLabelType> GetStationLabelTypes(Document doc)
    {
        var types = new List<StationLabelType>();
        if (doc is null || !CanCreateStationLabels)
        {
            return types;
        }

        var isValid = CivilAlignmentsBinding.LabelIsValidType;
        var isRecommended = CivilAlignmentsBinding.LabelIsRecommendedTypeForSet;
        if (isValid is null)
        {
            return types;
        }

        foreach (var type in new FilteredElementCollector(doc).OfClass(typeof(SpotDimensionType)).ToElements())
        {
            try
            {
                if (isValid.Invoke(null, new object[] { type }) is true)
                {
                    var recommended = isRecommended is not null && isRecommended.Invoke(null, new object[] { type }) is true;
                    types.Add(new StationLabelType(type.Id, type.Name, recommended));
                }
            }
            catch (Exception ex)
            {
                log.Debug(ex, "Could not check label type {name}", type.Name);
            }
        }

        return types.OrderByDescending(t => t.IsRecommended).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Creates a set of station labels along an alignment in a view and returns how many were made. Any argument left
    /// null uses Autodesk's default (the alignment's displayed range, 100 ft / 1000 m, 1/8 inch / 5 mm). When
    /// <paramref name="labelTypeId"/> is null or invalid, Autodesk's recommended label type is created. Lengths are in
    /// feet; the offset is a paper-space length, positive to the right of the alignment and negative to the left.
    /// </summary>
    public static int CreateStationLabels(Document doc, Element alignmentElement, View view, ElementId? labelTypeId,
        double? startStation, double? endStation, double? interval, double? offset, bool hasLeader)
    {
        if (doc is null)
        {
            throw new ArgumentNullException(nameof(doc));
        }

        if (alignmentElement is null)
        {
            throw new ArgumentNullException(nameof(alignmentElement));
        }

        if (view is null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        if (!CanCreateStationLabels)
        {
            throw new InvalidOperationException("The Civil Alignments add-in can't create station labels in this version of Revit.");
        }

        var alignment = GetAlignment(alignmentElement)
                        ?? throw new InvalidOperationException("The element is not a valid alignment.");

        var createSet = CivilAlignmentsBinding.LabelCreateSet
                        ?? throw new MissingMethodException("AlignmentStationLabel.CreateSet was not found.");

        using var transaction = new Transaction(doc, "Create station labels");
        transaction.Start();

        var failureHandlingOptions = transaction.GetFailureHandlingOptions();
        failureHandlingOptions.SetFailuresPreprocessor(new FailureHandler());
        transaction.SetFailureHandlingOptions(failureHandlingOptions);

        if (labelTypeId is null || labelTypeId == ElementId.InvalidElementId)
        {
            var create = CivilAlignmentsBinding.LabelCreateRecommendedTypeForSet;
            labelTypeId = create?.Invoke(null, new object[] { doc }) as ElementId ?? ElementId.InvalidElementId;
        }

        var options = Activator.CreateInstance(LabelSetOptionsType)
                      ?? throw new InvalidOperationException("Could not create the station label options.");
        SetOption(options, "HasLeader", hasLeader);
        SetOption(options, "TypeId", labelTypeId);
        SetOption(options, "StartStation", startStation);
        SetOption(options, "EndStation", endStation);
        SetOption(options, "Interval", interval);
        SetOption(options, "Offset", offset);

        object? labels;
        try
        {
            labels = createSet.Invoke(null, new[] { alignment, view, options });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
        }

        transaction.Commit();
        return labels is System.Collections.ICollection collection ? collection.Count : 0;
    }

    private static void SetOption(object options, string name, object? value)
    {
        if (value is not null)
        {
            LabelSetOptionsType.GetProperty(name, AnyMember)?.SetValue(options, value);
        }
    }

    /// <summary>
    /// Displayed end distance for a new alignment: Autodesk's own length for the JSON, unless it is missing or
    /// disagrees with the plan length (a units mismatch), in which case the plan length is used.
    /// </summary>
    private static double ResolveDisplayedEndDistance(string json, double planLength)
    {
        var autodeskLength = GetAlignmentLength(json);
        if (autodeskLength.HasValue && Math.Abs(autodeskLength.Value - planLength) <= Math.Max(0.01 * planLength, 1e-6))
        {
            return autodeskLength.Value;
        }

        log.Warning("Using plan length {planLength} as displayed end distance (Autodesk length: {autodeskLength})", planLength, autodeskLength);
        return planLength;
    }

    /// <summary>
    /// The name is already part of the JSON. Alignment.Name has no setter in current Revit versions, so only use it
    /// if it is writable, and also name the element's type so the alignment is easy to find in the browser.
    /// </summary>
    private static void ApplyName(Document doc, object alignment, Element? alignmentElement, string name)
    {
        try
        {
            var property = alignment.GetType().GetProperty("Name", AnyMember);
            if (property is not null && property.CanWrite)
            {
                property.SetValue(alignment, name);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not set the alignment name through the Name property");
        }

        try
        {
            if (alignmentElement is not null && doc.GetElement(alignmentElement.GetTypeId()) is ElementType type && type.Name != name)
            {
                type.Name = name;
            }
        }
        catch (Exception ex)
        {
            // for example another type already uses this name; the JSON name is still stored
            log.Debug(ex, "Could not name the alignment type");
        }
    }

    /// <summary>
    /// Alignment length (internal units) for a JSON document via the private
    /// Utilities.GetAlignmentLength(string), or null if it can't be determined.
    /// </summary>
    private static double? GetAlignmentLength(string json)
    {
        try
        {
            return CivilAlignmentsBinding.GetAlignmentLength?.Invoke(null, new object[] { json }) is double length ? length : null;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "GetAlignmentLength failed");
            return null;
        }
    }

    /// <summary>
    /// Reads points and directions along one alignment. Autodesk's public methods are looked up once and reused, and the
    /// stations are kept inside the alignment's displayed range (the methods throw outside it).
    /// </summary>
    internal sealed class PathReader
    {
        private readonly object _alignment;
        private readonly MethodInfo _pointAtStation;
        private readonly MethodInfo _tangentAtStation;
        private readonly MethodInfo? _curveEndpoints;

        public PathReader(Element alignmentElement)
        {
            if (alignmentElement is null)
            {
                throw new ArgumentNullException(nameof(alignmentElement));
            }

            if (!IsValid(alignmentElement))
            {
                throw new ArgumentException("The element is not a valid alignment.", nameof(alignmentElement));
            }

            _alignment = GetAlignment(alignmentElement)
                         ?? throw new InvalidOperationException("The alignment could not be read.");

            _pointAtStation = CivilAlignmentsBinding.GetPointAtStation
                              ?? throw new MissingMethodException("Alignment.GetPointAtStation was not found.");
            _tangentAtStation = CivilAlignmentsBinding.GetHCurveTangentAtStation
                                ?? throw new MissingMethodException("Alignment.GetHCurveTangentAtStation was not found.");
            _curveEndpoints = CivilAlignmentsBinding.GetDisplayedHorizontalCurveEndpoints;

            StartStation = ReadProperty<double>(_alignment, "DisplayedStartStation");
            EndStation = ReadProperty<double>(_alignment, "DisplayedEndStation");
        }

        /// <summary>First and last displayed station, in feet.</summary>
        public double StartStation { get; }
        public double EndStation { get; }

        /// <summary>The 3D point on the alignment at a station (plan position and profile elevation), in model coordinates.</summary>
        public XYZ PointAt(double station) => Invoke(_pointAtStation, station);

        /// <summary>The horizontal direction of travel at a station as a unit vector (Z is 0).</summary>
        public XYZ HorizontalTangentAt(double station) => Invoke(_tangentAtStation, station);

        /// <summary>Stations where one horizontal curve ends and the next begins.</summary>
        public IList<double> HorizontalCurveStations()
        {
            var stations = new List<double>();
            if (_curveEndpoints?.Invoke(_alignment, null) is System.Collections.IEnumerable endpoints)
            {
                foreach (var endpoint in endpoints)
                {
                    stations.Add(ReadProperty<double>(endpoint, "Station"));
                }
            }

            return stations;
        }

        private XYZ Invoke(MethodInfo method, double station)
        {
            var clamped = Math.Max(StartStation, Math.Min(EndStation, station));
            try
            {
                return (XYZ?)method.Invoke(_alignment, new object[] { clamped })
                       ?? throw new InvalidOperationException("The alignment returned no value at the station.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
            }
        }
    }

    private static T? ReadProperty<T>(object instance, string name)
    {
        var value = instance.GetType().GetProperty(name, AnyMember)?.GetValue(instance);
        return value is T typed ? typed : default;
    }
}

internal class AlignmentInfo
{
    public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double DisplayedStartStation { get; set; }
    public double DisplayedEndStation { get; set; }
}
