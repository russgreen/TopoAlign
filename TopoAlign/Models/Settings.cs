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
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", false))
            {
                if (key != null)
                {
                    SingleElement = Convert.ToBoolean(key.GetValue("SingleElement", SingleElement));
                    CleanTopoPoints = Convert.ToBoolean(key.GetValue("CleanTopoPoints", CleanTopoPoints));
                    TopFace = Convert.ToBoolean(key.GetValue("TopFace", TopFace));
                    CreateAlignmentReverseDirection = Convert.ToBoolean(key.GetValue("CreateAlignmentReverseDirection", CreateAlignmentReverseDirection));
                    CreateAlignmentDeleteSourceLines = Convert.ToBoolean(key.GetValue("CreateAlignmentDeleteSourceLines", CreateAlignmentDeleteSourceLines));
                    CreateAlignmentDrapeOnTopo = Convert.ToBoolean(key.GetValue("CreateAlignmentDrapeOnTopo", CreateAlignmentDrapeOnTopo));

                    _ = decimal.TryParse(key.GetValue("DivideEdgeDistance", DivideEdgeDistance).ToString(), out decimal divideEdgeDistance);
                    _ = decimal.TryParse(key.GetValue("VerticalOffset", VerticalOffset).ToString(), out decimal verticalOffset);
                    _ = decimal.TryParse(key.GetValue("CreateAlignmentStartStation", CreateAlignmentStartStation).ToString(), out decimal createAlignmentStartStation);

                    CreateAlignmentSampleInterval = ReadDecimal(key, "CreateAlignmentSampleInterval", CreateAlignmentSampleInterval);
                    CreateAlignmentVerticalTolerance = ReadDecimal(key, "CreateAlignmentVerticalTolerance", CreateAlignmentVerticalTolerance);
                    CreateAlignmentElevationOffset = ReadDecimal(key, "CreateAlignmentElevationOffset", CreateAlignmentElevationOffset);
                    CreateAlignmentMinVerticalCurveLength = ReadDecimal(key, "CreateAlignmentMinVerticalCurveLength", CreateAlignmentMinVerticalCurveLength);

                    DivideEdgeDistance = divideEdgeDistance;
                    VerticalOffset = verticalOffset;
                    CreateAlignmentStartStation = createAlignmentStartStation;
                }
            }
    }

    public void SaveSettings()
    {
        Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", true);
        if (key == null)
        {
            Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\RGTools\TopoAlign", true);
            key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", true);
        }

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
            key.SetValue("DivideEdgeDistance", DivideEdgeDistance, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("VerticalOffset", VerticalOffset, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentStartStation", CreateAlignmentStartStation, Microsoft.Win32.RegistryValueKind.String);
   
    }

}
