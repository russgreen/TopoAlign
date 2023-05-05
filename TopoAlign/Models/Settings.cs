using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign.Models;

public class Settings //: INotifyPropertyChanged
{
    public bool SingleElement { get; set; } = true;
    public bool CleanTopoPoints { get; set; } = true;
    public bool TopFace { get; set; } = true;
    public decimal DivideEdgeDistance { get; set; } = Convert.ToDecimal(16.4041994750656d); // 5000
    public decimal VerticalOffset { get; set; } = Convert.ToDecimal(0.164041994750656d); // 50

    public void LoadSettings()
    {               
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\RGTools\TopoAlign", false))
            {
                if (key != null)
                {
                    SingleElement = Convert.ToBoolean(key.GetValue("SingleElement", SingleElement));
                    CleanTopoPoints = Convert.ToBoolean(key.GetValue("CleanTopoPoints", CleanTopoPoints));
                    TopFace = Convert.ToBoolean(key.GetValue("TopFace", TopFace));

                    _ = decimal.TryParse(key.GetValue("DivideEdgeDistance", DivideEdgeDistance).ToString(), out decimal divideEdgeDistance);
                    _ = decimal.TryParse(key.GetValue("VerticalOffset", VerticalOffset).ToString(), out decimal verticalOffset);

                    //DivideEdgeDistance = Convert.ToDecimal(key.GetValue("DivideEdgeDistance", DivideEdgeDistance));
                    //VerticalOffset = Convert.ToDecimal(key.GetValue("VerticalOffset", VerticalOffset));
                    DivideEdgeDistance = divideEdgeDistance;
                    VerticalOffset = verticalOffset;

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
            key.SetValue("DivideEdgeDistance", DivideEdgeDistance, Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("VerticalOffset", VerticalOffset, Microsoft.Win32.RegistryValueKind.String);
   
    }

}
