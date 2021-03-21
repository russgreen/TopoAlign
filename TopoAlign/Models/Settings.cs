using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopoAlign.Models
{
    public class Settings //: INotifyPropertyChanged
    {
        //public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public bool SingleElement { get; set; } = true;
        public bool CleanTopoPoints { get; set; } = true;
        public bool TopFace { get; set; } = true;
        public decimal DivideEdgeDistance { get; set; } = Convert.ToDecimal(16.4041994750656d); // 5000
        public decimal VerticalOffset { get; set; } = Convert.ToDecimal(0.164041994750656d); // 50

        public void LoadSettings()
        {               
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Archisoft\TopoAlign", false))
                {
                    if (key != null)
                    {
                        SingleElement = Convert.ToBoolean(key.GetValue("SingleElement", SingleElement));
                        CleanTopoPoints = Convert.ToBoolean(key.GetValue("CleanTopoPoints", CleanTopoPoints));
                        TopFace = Convert.ToBoolean(key.GetValue("TopFace", TopFace));
                        DivideEdgeDistance = Convert.ToDecimal(key.GetValue("DivideEdgeDistance", DivideEdgeDistance));
                        VerticalOffset = Convert.ToDecimal(key.GetValue("VerticalOffset", VerticalOffset));
                    }


                }
        }

        public void SaveSettings()
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Archisoft\TopoAlign", true))
            {
                if (key == null)
                {
                   Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Archisoft\TopoAlign", true);
                }

                key.SetValue("SingleElement", SingleElement, Microsoft.Win32.RegistryValueKind.String);
                key.SetValue("CleanTopoPoints", CleanTopoPoints, Microsoft.Win32.RegistryValueKind.String);
                key.SetValue("TopFace", TopFace, Microsoft.Win32.RegistryValueKind.String);
                key.SetValue("DivideEdgeDistance", DivideEdgeDistance, Microsoft.Win32.RegistryValueKind.String);
                key.SetValue("VerticalOffset", VerticalOffset, Microsoft.Win32.RegistryValueKind.String);
            }
        }

    }
}
