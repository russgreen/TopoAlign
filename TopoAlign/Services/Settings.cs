using Autodesk.Revit.DB;

namespace TopoAlign.Services;

public class Settings
{
    public bool SingleElement { get; set; } = true;
    public bool CleanTopoPoints { get; set; } = true;
    public bool TopFace { get; set; } = true;
    public bool CreateAlignmentReverseDirection { get; set; }
    public bool CreateAlignmentDeleteSourceLines { get; set; }

    // Profile draping options, all lengths in feet (internal units).
    public bool CreateAlignmentDrapeOnTopo { get; set; }
    public decimal CreateAlignmentSampleInterval { get; set; } = Convert.ToDecimal(1.0 / 0.3048);        // 1 m
    public decimal CreateAlignmentVerticalTolerance { get; set; } = Convert.ToDecimal(0.05 / 0.3048);    // 50 mm
    public decimal CreateAlignmentElevationOffset { get; set; }
    public decimal CreateAlignmentMinVerticalCurveLength { get; set; }

    // Transition spirals and point (PI) input, lengths in feet (internal units).
    public bool CreateAlignmentAddSpirals { get; set; }
    public decimal CreateAlignmentSpiralLength { get; set; } = Convert.ToDecimal(30.0 / 0.3048);         // 30 m
    public decimal CreateAlignmentDefaultRadius { get; set; } = Convert.ToDecimal(100.0 / 0.3048);       // 100 m

    // Sweep along alignment (roads and paths), lengths in feet (internal units).
    public decimal SweepInterval { get; set; } = Convert.ToDecimal(1.0 / 0.3048);                        // 1 m
    public decimal SweepVerticalOffset { get; set; }
    public bool SweepMirrorProfile { get; set; }

    // Edit profile, length in feet (internal units).
    public decimal EditProfileCurveLength { get; set; } = Convert.ToDecimal(20.0 / 0.3048);              // 20 m

    /// <summary>Name of the last Revit category used for a road, such as OST_Roads; blank for the first one available.</summary>
    public string SweepCategory { get; set; } = string.Empty;

    /// <summary>Name of the last profile type used, as "Family : Type".</summary>
    public string SweepProfileName { get; set; } = string.Empty;

    /// <summary>Whether the sweep makes a single profile ("Profile") or an assembly of profiles ("Assembly").</summary>
    public string SweepMode { get; set; } = "Profile";

    /// <summary>Name of the last assembly used.</summary>
    public string SweepAssemblyName { get; set; } = string.Empty;

    /// <summary>Whether sweeping an assembly also grades the picked toposolid out from its sides.</summary>
    public bool SweepGradeTopo { get; set; }

    /// <summary>How the road solid is made: SegmentedLoft or SmoothSurfaces.</summary>
    public string SweepGeometryMethod { get; set; } = "SegmentedLoft";

    /// <summary>What the road is made into: DirectShape or Family.</summary>
    public string SweepOutput { get; set; } = "DirectShape";

    /// <summary>Name of the last toposolid type used when converting a road to a toposolid.</summary>
    public string RoadToposolidType { get; set; } = string.Empty;

    /// <summary>Whether converting a road to a toposolid also deletes the road.</summary>
    public bool RoadToposolidDeleteRoad { get; set; }

    // Grade to surface, lengths in feet (internal units).
    public string GradeSource { get; set; } = "Edges";
    public string GradeSlopeMode { get; set; } = "Ratio";
    public decimal GradeSlopeValue { get; set; } = 3;
    public decimal GradeMaxWidth { get; set; } = Convert.ToDecimal(50.0 / 0.3048);                       // 50 m
    public decimal GradeVerticalOffset { get; set; }
    public decimal GradeInterval { get; set; } = Convert.ToDecimal(1.0 / 0.3048);                        // 1 m
    public bool GradeAddEdgePoints { get; set; } = true;

    // T-junction, lengths in feet (internal units).
    public decimal TJunctionKerbRadius { get; set; } = Convert.ToDecimal(6.0 / 0.3048);                  // 6 m
    public decimal TJunctionSideHalfWidth { get; set; } = Convert.ToDecimal(3.0 / 0.3048);               // 3 m
    public decimal TJunctionSurfaceThickness { get; set; } = Convert.ToDecimal(0.15 / 0.3048);           // 150 mm
    public bool TJunctionCreateSurface { get; set; } = true;
    public decimal TJunctionMainLevelOffset { get; set; }
    public decimal TJunctionMainHalfWidth { get; set; } = Convert.ToDecimal(3.0 / 0.3048);               // 3 m

    // Kerb sweep along picked edges, lengths in feet (internal units).
    public decimal KerbSweepInterval { get; set; } = Convert.ToDecimal(1.0 / 0.3048);                    // 1 m
    public decimal KerbSweepVerticalOffset { get; set; }
    public bool KerbSweepFlip { get; set; }
    public bool KerbSweepReview { get; set; } = true;
    public string KerbSweepCategory { get; set; } = string.Empty;
    public string KerbSweepProfileName { get; set; } = string.Empty;
    public string KerbSweepOutput { get; set; } = "DirectShape";

    // Dropped kerb ramp, lengths in feet (internal units).
    public decimal KerbRampWidth { get; set; } = 4;
    public decimal KerbRampGradient { get; set; } = 12;
    public decimal KerbFlareGradient { get; set; } = 12;
    public decimal KerbRampMaxLength { get; set; } = 10;
    public decimal KerbRampLevelOffset { get; set; }

#if REVIT2021_OR_GREATER
    public decimal CreateAlignmentStartStation { get; set; } = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(0, UnitTypeId.Meters));
#else
    public decimal CreateAlignmentStartStation { get; set; } = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(0, DisplayUnitType.DUT_METERS));
#endif

#if REVIT2021_OR_GREATER
    public decimal DivideEdgeDistance { get; set; } = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters)); // 5000
    public decimal VerticalOffset { get; set; } = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters)); // 50
#else
    public decimal DivideEdgeDistance { get; set; } = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(5000, DisplayUnitType.DUT_MILLIMETERS)); // 5000
    public decimal VerticalOffset { get; set; } = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits(50, DisplayUnitType.DUT_MILLIMETERS)); // 50
#endif

    // ---- persistence: %AppData%\TopoAlign\settings.json ------------------------------------------------------

    /// <summary>Where the settings are stored. Tests point this at a temporary file.</summary>
    internal static string FilePath { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TopoAlign", "settings.json");

    /// <summary>The HKCU key older versions stored the settings in. Only read once, to migrate them to the JSON file.</summary>
    internal const string DefaultLegacyRegistryKeyPath = @"SOFTWARE\RGTools\TopoAlign";
    internal static string LegacyRegistryKeyPath { get; set; } = DefaultLegacyRegistryKeyPath;

    private static readonly System.Text.Json.JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    /// <summary>Every public bool, decimal and string property is a setting.</summary>
    private static IEnumerable<System.Reflection.PropertyInfo> SettingProperties()
    {
        return typeof(Settings)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                        && (p.PropertyType == typeof(bool) || p.PropertyType == typeof(decimal) || p.PropertyType == typeof(string)));
    }

    /// <summary>
    /// Loads the settings from the JSON file. Missing, blank or malformed values keep their defaults, and a missing or
    /// unreadable file leaves every default. If there is no file yet but the old registry key exists, its values are
    /// moved into the JSON file and the key is removed.
    /// </summary>
    public void LoadSettings()
    {
        try
        {
            if (System.IO.File.Exists(FilePath))
            {
                ApplyJson(System.IO.File.ReadAllText(FilePath));
                return;
            }

            if (LoadLegacyRegistry() && TrySave())
            {
                DeleteLegacyRegistry();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not load the TopoAlign settings from {Path}", FilePath);
        }
    }

    public void SaveSettings()
    {
        _ = TrySave();
    }

    private bool TrySave()
    {
        try
        {
            var root = new System.Text.Json.Nodes.JsonObject();
            foreach (var property in SettingProperties())
            {
                var value = property.GetValue(this);
                root[property.Name] = value switch
                {
                    bool b => System.Text.Json.Nodes.JsonValue.Create(b),
                    decimal d => System.Text.Json.Nodes.JsonValue.Create(d),
                    _ => System.Text.Json.Nodes.JsonValue.Create(value as string ?? string.Empty)
                };
            }

            var directory = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // write beside the target and swap it in, so a crash never leaves a half-written settings file
            var temp = FilePath + ".tmp";
            System.IO.File.WriteAllText(temp, root.ToJsonString(_writeOptions), new System.Text.UTF8Encoding(false));
            if (System.IO.File.Exists(FilePath))
            {
                System.IO.File.Replace(temp, FilePath, null);
            }
            else
            {
                System.IO.File.Move(temp, FilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not save the TopoAlign settings to {Path}", FilePath);
            return false;
        }
    }

    private void ApplyJson(string json)
    {
        if (System.Text.Json.Nodes.JsonNode.Parse(json) is not System.Text.Json.Nodes.JsonObject root)
        {
            return;
        }

        foreach (var property in SettingProperties())
        {
            if (root.TryGetPropertyValue(property.Name, out var node) && node is System.Text.Json.Nodes.JsonValue jsonValue
                && TryConvert(jsonValue, property.PropertyType, out var value))
            {
                property.SetValue(this, value);
            }
        }
    }

    private static bool TryConvert(System.Text.Json.Nodes.JsonValue node, Type type, out object? value)
    {
        value = null;

        if (type == typeof(bool))
        {
            if (node.TryGetValue(out bool b)) { value = b; return true; }
            if (node.TryGetValue(out string? s) && bool.TryParse(s, out b)) { value = b; return true; }
        }
        else if (type == typeof(decimal))
        {
            if (node.TryGetValue(out decimal d)) { value = d; return true; }
            if (node.TryGetValue(out string? s)
                && decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out d))
            {
                value = d;
                return true;
            }
        }
        else if (node.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        return false;
    }

    // ---- one-time migration from the old registry location ------------------------------------------------------

    /// <summary>Reads the old registry values (written as culture-formatted strings). Returns true if the key existed.</summary>
    private bool LoadLegacyRegistry()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(LegacyRegistryKeyPath, false);
        if (key is null)
        {
            return false;
        }

        foreach (var property in SettingProperties())
        {
            var raw = key.GetValue(property.Name)?.ToString();
            if (raw is null)
            {
                continue;
            }

            if (property.PropertyType == typeof(bool) && bool.TryParse(raw, out var b))
            {
                property.SetValue(this, b);
            }
            else if (property.PropertyType == typeof(decimal) && decimal.TryParse(raw, out var d))
            {
                property.SetValue(this, d);
            }
            else if (property.PropertyType == typeof(string) && !string.IsNullOrWhiteSpace(raw))
            {
                property.SetValue(this, raw);
            }
        }

        return true;
    }

    private static void DeleteLegacyRegistry()
    {
        try
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(LegacyRegistryKeyPath, false);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not remove the old TopoAlign registry settings");
        }
    }
}
