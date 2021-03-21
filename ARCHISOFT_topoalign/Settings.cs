using System;
using System.ComponentModel;
using Microsoft.Win32;

namespace ARCHISOFT_topoalign
{
    public class Settings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public delegate void PropertyChangedEventHandler(object sender, PropertyChangedEventArgs e);

        private bool m_SingleElement;
        private bool m_CleanTopoPoints;
        private bool m_TopFace;
        private decimal m_DivideEdgeDistance;
        private decimal m_VerticalOffset;

        public bool SingleElement
        {
            get
            {
                return m_SingleElement;
            }

            set
            {
                m_SingleElement = value;
            }
        }

        public bool CleanTopoPoints
        {
            get
            {
                return m_CleanTopoPoints;
            }

            set
            {
                m_CleanTopoPoints = value;
            }
        }

        public bool TopFace
        {
            get
            {
                return m_TopFace;
            }

            set
            {
                m_TopFace = value;
            }
        }

        public decimal DivideEdgeDistance
        {
            get
            {
                return m_DivideEdgeDistance;
            }

            set
            {
                m_DivideEdgeDistance = value;
            }
        }

        public decimal VerticalOffset
        {
            get
            {
                return m_VerticalOffset;
            }

            set
            {
                m_VerticalOffset = value;
            }
        }

        public Settings()
        {
            m_SingleElement = true;
            m_CleanTopoPoints = true;
            m_TopFace = true;
            m_DivideEdgeDistance = Convert.ToDecimal(16.4041994750656d); // 5000
            m_VerticalOffset = Convert.ToDecimal(0.164041994750656d); // 50
        }

        public void LoadSettings()
        {
            try
            {
                if (My.MyProject.Computer.Registry.CurrentUser.OpenSubKey(@"Software\Archisoft\TopoAlign") is object)
                {
                    {
                        var withBlock = My.MyProject.Computer.Registry.CurrentUser.OpenSubKey(@"Software\Archisoft\TopoAlign");
                        m_SingleElement = Convert.ToBoolean(withBlock.GetValue("SingleElement", m_SingleElement));
                        m_CleanTopoPoints = Convert.ToBoolean(withBlock.GetValue("CleanTopoPoints", m_CleanTopoPoints));
                        m_TopFace = Convert.ToBoolean(withBlock.GetValue("TopFace", m_TopFace));
                        m_DivideEdgeDistance = Convert.ToDecimal(withBlock.GetValue("DivideEdgeDistance", m_DivideEdgeDistance));
                        m_VerticalOffset = Convert.ToDecimal(withBlock.GetValue("VerticalOffset", m_VerticalOffset));
                    }
                }
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
            }
            finally
            {
                My.MyProject.Computer.Registry.CurrentUser.Close();
            }
        }

        public void SaveSettings()
        {
            if (My.MyProject.Computer.Registry.CurrentUser.OpenSubKey(@"Software\Archisoft\TopoAlign") is null)
            {
                My.MyProject.Computer.Registry.CurrentUser.CreateSubKey(@"Software\Archisoft\TopoAlign");
            }

            {
                var withBlock = My.MyProject.Computer.Registry.CurrentUser.OpenSubKey(@"Software\Archisoft\TopoAlign", true);
                withBlock.SetValue("SingleElement", m_SingleElement, RegistryValueKind.String);
                withBlock.SetValue("CleanTopoPoints", m_CleanTopoPoints, RegistryValueKind.String);
                withBlock.SetValue("TopFace", m_TopFace, RegistryValueKind.String);
                withBlock.SetValue("DivideEdgeDistance", m_DivideEdgeDistance, RegistryValueKind.String);
                withBlock.SetValue("VerticalOffset", m_VerticalOffset, RegistryValueKind.String);
            }
        }
    }
}