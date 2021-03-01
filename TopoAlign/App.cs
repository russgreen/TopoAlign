#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

#endregion

namespace TopoAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    class App : IExternalApplication
    {
        public static UIControlledApplication cachedUiCtrApp;

        public static Autodesk.Revit.DB.Document revitDocument;

        public Result OnStartup(UIControlledApplication a)
        {
            cachedUiCtrApp = a;
            var ribbonPanel = CreateRibbonPanel();

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }

        private RibbonPanel CreateRibbonPanel()
        {
            RibbonPanel panel;

            // Check if "Archisoft Tools already exists and use if its there
            try
            {
                panel = cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString());
                panel.Name = "ARBG_Transmittal_ExtApp";
                panel.Title = "eProject Transmittal";
            }
            catch
            {
                var archisoftPanel = false;
                var pluginPath = @"C:\ProgramData\Autodesk\ApplicationPlugins";
                if (System.IO.Directory.Exists(pluginPath) == true)
                {
                    foreach (var folder in System.IO.Directory.GetDirectories(pluginPath))
                    {
                        if(folder.ToLower().Contains("archisoft") == true & folder.ToLower().Contains("archisoft topoalign") == false)
                        {
                            archisoftPanel = true;
                            break;
                        }
                    }

                    if(archisoftPanel == true)
                    {
                        cachedUiCtrApp.CreateRibbonTab("Archisoft Tools");
                        panel = cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString());
                        panel.Name = "ARBG_Transmittal_ExtApp";
                        panel.Title = "eProject Transmittal";
                    }
                    else
                    {
                        panel = cachedUiCtrApp.CreateRibbonPanel("Topo Align");
                    }
                }


            }

            return panel;
        }
        private System.Windows.Media.ImageSource PngImageSource(string embeddedPath)
        {
            var stream = GetType().Assembly.GetManifestResourceStream(embeddedPath);
            System.Windows.Media.ImageSource imageSource;
            try
            {
                imageSource = BitmapFrame.Create(stream);
            }
            catch
            {
                imageSource = null;
            }

            return imageSource;
        }
    }
}
