using Autodesk.Revit.DB;

namespace TopoAlign.Models;

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

    /// <summary>How the road solid is made: SegmentedLoft or SmoothSurfaces.</summary>
    public string SweepGeometryMethod { get; set; } = "SegmentedLoft";

    /// <summary>What the road is made into: DirectShape or Family.</summary>
    public string SweepOutput { get; set; } = "DirectShape";

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
    private static decimal ReadDecimal(Microsoft.Win32.RegistryKey key, string name, decimal fallback)
    {
        return decimal.TryParse(key.GetValue(name, fallback)?.ToString(), out var value) ? value : fallback;
    }

    public void LoadSettings()
    {               
            using (Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", false))
            {
                if (key != null)
                {
                    SingleElement = Convert.ToBoolean(key.GetValue("SingleElement", SingleElement));
                    CleanTopoPoints = Convert.ToBoolean(key.GetValue("CleanTopoPoints", CleanTopoPoints));
                    TopFace = Convert.ToBoolean(key.GetValue("TopFace", TopFace));
                    CreateAlignmentReverseDirection = Convert.ToBoolean(key.GetValue("CreateAlignmentReverseDirection", CreateAlignmentReverseDirection));
                    CreateAlignmentDeleteSourceLines = Convert.ToBoolean(key.GetValue("CreateAlignmentDeleteSourceLines", CreateAlignmentDeleteSourceLines));
                    CreateAlignmentDrapeOnTopo = Convert.ToBoolean(key.GetValue("CreateAlignmentDrapeOnTopo", CreateAlignmentDrapeOnTopo));
                    CreateAlignmentAddSpirals = Convert.ToBoolean(key.GetValue("CreateAlignmentAddSpirals", CreateAlignmentAddSpirals));
                    SweepMirrorProfile = Convert.ToBoolean(key.GetValue("SweepMirrorProfile", SweepMirrorProfile));
                    SweepCategory = Convert.ToString(key.GetValue("SweepCategory", SweepCategory)) ?? string.Empty;
                    SweepProfileName = Convert.ToString(key.GetValue("SweepProfileName", SweepProfileName)) ?? string.Empty;
                    SweepGeometryMethod = Convert.ToString(key.GetValue("SweepGeometryMethod", SweepGeometryMethod)) ?? "SegmentedLoft";
                    SweepOutput = Convert.ToString(key.GetValue("SweepOutput", SweepOutput)) ?? "DirectShape";

                    _ = decimal.TryParse(key.GetValue("DivideEdgeDistance", DivideEdgeDistance).ToString(), out decimal divideEdgeDistance);
                    _ = decimal.TryParse(key.GetValue("VerticalOffset", VerticalOffset).ToString(), out decimal verticalOffset);
                    _ = decimal.TryParse(key.GetValue("CreateAlignmentStartStation", CreateAlignmentStartStation).ToString(), out decimal createAlignmentStartStation);

                    CreateAlignmentSampleInterval = ReadDecimal(key, "CreateAlignmentSampleInterval", CreateAlignmentSampleInterval);
                    CreateAlignmentVerticalTolerance = ReadDecimal(key, "CreateAlignmentVerticalTolerance", CreateAlignmentVerticalTolerance);
                    CreateAlignmentElevationOffset = ReadDecimal(key, "CreateAlignmentElevationOffset", CreateAlignmentElevationOffset);
                    CreateAlignmentMinVerticalCurveLength = ReadDecimal(key, "CreateAlignmentMinVerticalCurveLength", CreateAlignmentMinVerticalCurveLength);
                    CreateAlignmentSpiralLength = ReadDecimal(key, "CreateAlignmentSpiralLength", CreateAlignmentSpiralLength);
                    CreateAlignmentDefaultRadius = ReadDecimal(key, "CreateAlignmentDefaultRadius", CreateAlignmentDefaultRadius);
                    SweepInterval = ReadDecimal(key, "SweepInterval", SweepInterval);
                    SweepVerticalOffset = ReadDecimal(key, "SweepVerticalOffset", SweepVerticalOffset);
                    EditProfileCurveLength = ReadDecimal(key, "EditProfileCurveLength", EditProfileCurveLength);

                    DivideEdgeDistance = divideEdgeDistance;
                    VerticalOffset = verticalOffset;
                    CreateAlignmentStartStation = createAlignmentStartStation;
                }
            }
    }

    public void SaveSettings()
    {
        Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", true)
                                          ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\RGTools\TopoAlign", true);

            key.SetValue("SingleElement", SingleElement, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CleanTopoPoints", CleanTopoPoints, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("TopFace", TopFace, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentReverseDirection", CreateAlignmentReverseDirection, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentDeleteSourceLines", CreateAlignmentDeleteSourceLines, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentDrapeOnTopo", CreateAlignmentDrapeOnTopo, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentSampleInterval", CreateAlignmentSampleInterval, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentVerticalTolerance", CreateAlignmentVerticalTolerance, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentElevationOffset", CreateAlignmentElevationOffset, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentMinVerticalCurveLength", CreateAlignmentMinVerticalCurveLength, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentAddSpirals", CreateAlignmentAddSpirals, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentSpiralLength", CreateAlignmentSpiralLength, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentDefaultRadius", CreateAlignmentDefaultRadius, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepInterval", SweepInterval, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepVerticalOffset", SweepVerticalOffset, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("EditProfileCurveLength", EditProfileCurveLength, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepMirrorProfile", SweepMirrorProfile, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepCategory", SweepCategory ?? string.Empty, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepProfileName", SweepProfileName ?? string.Empty, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepGeometryMethod", SweepGeometryMethod ?? "SegmentedLoft", Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("SweepOutput", SweepOutput ?? "DirectShape", Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("DivideEdgeDistance", DivideEdgeDistance, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("VerticalOffset", VerticalOffset, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentStartStation", CreateAlignmentStartStation, Microsoft.Win32.RegistryValueKind.String);
   
    }

}
