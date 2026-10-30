using Autodesk.Revit.DB;

namespace TopoAlign.Models;

public class Settings
{
    public bool SingleElement { get; set; } = true;
    public bool CleanTopoPoints { get; set; } = true;
    public bool TopFace { get; set; } = true;
    public bool CreateAlignmentReverseDirection { get; set; }
    public bool CreateAlignmentDeleteSourceLines { get; set; }

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

                    _ = decimal.TryParse(key.GetValue("DivideEdgeDistance", DivideEdgeDistance).ToString(), out decimal divideEdgeDistance);
                    _ = decimal.TryParse(key.GetValue("VerticalOffset", VerticalOffset).ToString(), out decimal verticalOffset);
                    _ = decimal.TryParse(key.GetValue("CreateAlignmentStartStation", CreateAlignmentStartStation).ToString(), out decimal createAlignmentStartStation);

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
            key.SetValue("DivideEdgeDistance", DivideEdgeDistance, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("VerticalOffset", VerticalOffset, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("CreateAlignmentStartStation", CreateAlignmentStartStation, Microsoft.Win32.RegistryValueKind.String);
   
    }

}
