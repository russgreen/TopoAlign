/* TODO ERROR: Skipped RegionDirectiveTrivia */using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ARCHISOFT_topoalign
{
    // Imports Microsoft.AppCenter
    // Imports Microsoft.AppCenter.Crashes
    /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    class AdskApplication : IExternalApplication
    {
        /// <summary>
    /// This method is called when Revit starts up before a
    /// document or default template is actually loaded.
    /// </summary>
    /// <param name="app">An object passed to the external
    /// application which contains the controlled application.</param>
    /// <returns>Return the status of the external application.
    /// A result of Succeeded means that the external application started successfully.
    /// Cancelled can be used to signify a problem. If so, Revit informs the user that
    /// the external application failed to load and releases the internal reference.
    /// </returns>

        public static UIControlledApplication _cachedUiCtrApp;
        // Public Shared elog As New EventLogger

        // #If CONFIG = "2015" Then
        // Public Shared elog As New EventLogger("2015")
        // #ElseIf CONFIG = "2016" Then
        // Public Shared elog As New EventLogger("2016")
        // #End If

        public Result OnStartup(UIControlledApplication app)
        {

            // AppCenter.LogLevel = LogLevel.Verbose
            // Crashes.ShouldAwaitUserConfirmation = Function()
            // ' Build your own UI to ask for user consent here. SDK doesn't provide one by default.
            // Dim dialog = New DialogUserConfirmation()

            // If dialog.ShowDialog() = System.Windows.Forms.DialogResult.None Then
            // Crashes.NotifyUserConfirmation(dialog.ClickResult)
            // End If

            // ' Return true if you built a UI for user consent and are waiting for user input on that custom UI, otherwise false.
            // Return True
            // End Function

            // AppCenter.Start("c26c8f38-0aad-44c7-9064-478429495727", GetType(Crashes))

            try
            {
                // Add your code here
                _cachedUiCtrApp = app;
                var ribbonPanel = CreateRibbonPanel();

                // Return Success
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Crashes.TrackError(ex)

                return Result.Failed;
            }
        }


        /// <summary>
    /// This method is called when Revit is about to exit.
    /// All documents are closed before this method is called.
    /// </summary>
    /// <param name="app">An object passed to the external
    /// application which contains the controlled application.</param>
    /// <returns>Return the status of the external application.
    /// A result of Succeeded means that the external application successfully shutdown.
    /// Cancelled can be used to signify that the user cancelled the external operation
    /// at some point. If false is returned then the Revit user should be warned of the
    /// failure of the external application to shut down correctly.</returns>
        public Result OnShutdown(UIControlledApplication app)
        {

            // TODO: Add shutdown code here

            // Must return some code
            return Result.Succeeded;
        }

        public RibbonPanel CreateRibbonPanel()
        {
            RibbonPanel panel;

            // Check if "Archisoft Tools already exists and use if its there
            try
            {
                panel = _cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString());
                panel.Name = "ARBG_TopoAlign_ExtApp";
                panel.Title = "Topo Align";
            }
            catch
            {
                bool ArchisoftPanel = false;
                if (My.MyProject.Computer.FileSystem.DirectoryExists(@"C:\ProgramData\Autodesk\ApplicationPlugins") == true)
                {
                    foreach (var folder in My.MyProject.Computer.FileSystem.GetDirectories(@"C:\ProgramData\Autodesk\ApplicationPlugins"))
                    {
                        if (folder.ToLower().Contains("archisoft") == true & folder.ToLower().Contains("archisoft topoalign") == false)
                        {
                            ArchisoftPanel = true;
                            break;
                        }
                    }
                }

                if (ArchisoftPanel == true)
                {
                    _cachedUiCtrApp.CreateRibbonTab("Archisoft Tools");
                    panel = _cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString());
                    panel.Name = "ARBG_TopoAlign_ExtApp";
                    panel.Title = "Topo Align";
                }
                else
                {
                    panel = _cachedUiCtrApp.CreateRibbonPanel("Topo Align");
                }
            }

            var pbDataTopoAlign = new PushButtonData("Align to Element", "Align to Element", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdAlignTopo");
            PushButton pbTopoAlign = (PushButton)panel.AddItem(pbDataTopoAlign);
            pbTopoAlign.ToolTip = "Adjust topo to edge or floor geometry";
            pbTopoAlign.LargeImage = RetriveImage("ARCHISOFT_topoalign.TopoAlign32.png");
            // pbTopoAlign.Image = RetriveImage("ARCHISOFT_topoalign.TopoAlign16x16.bmp")

            var pbDataPointsFromLines = new PushButtonData("Points from Lines", "Points from Lines", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdPointsOnSurface");
            PushButton pbPointsFromLines = (PushButton)panel.AddItem(pbDataPointsFromLines);
            pbPointsFromLines.ToolTip = "Add points on surface along selected model lines. Model lines must be lines and arcs and be BELOW the topo surface.";
            pbPointsFromLines.LargeImage = RetriveImage("ARCHISOFT_topoalign.PointsFromLines32.png");
            // pbPointsFromLines.Image = RetriveImage("ARCHISOFT_topoalign.TopoPoints16x16.bmp")

            var pbDataPointsAlongContours = new PushButtonData("Points along contours", "Points along contours", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdPointsAlongContours");
            PushButton pbPointsAlongContours = (PushButton)panel.AddItem(pbDataPointsAlongContours);
            pbPointsAlongContours.ToolTip = "Add points on surface along selected contour model lines";
            pbPointsAlongContours.LargeImage = RetriveImage("ARCHISOFT_topoalign.PointsFromContours32.png");
            // pbPointsAlongContours.Image = RetriveImage("ARCHISOFT_topoalign.TopoPoints16x16.bmp")

            var pbDataResetRegion = new PushButtonData("Reset region", "Reset region", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdResetTopoRegion");
            PushButton pbResetRegion = (PushButton)panel.AddItem(pbDataResetRegion);
            pbResetRegion.ToolTip = "Copy points from existing topo surface to new topo surface within a region to undo changes made.";
            pbResetRegion.LargeImage = RetriveImage("ARCHISOFT_topoalign.Reset32.png");
            // pbResetRegion.Image = RetriveImage("ARCHISOFT_topoalign.TopoReset16x16.bmp")

            // Help document
            ContextualHelp contextHelp;
            /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElifDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElifDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElifDirectiveTrivia */
            contextHelp = new ContextualHelp(ContextualHelpType.Url, "https://apps.autodesk.com/RVT/en/Detail/HelpDoc?appId=3561777884450830300&appLang=en&os=Win64");
            /* TODO ERROR: Skipped ElifDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped EndIfDirectiveTrivia */
            pbTopoAlign.SetContextualHelp(contextHelp);
            return default;
        }

        private static ImageSource RetriveImage(string imagePath)
        {
            var manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(imagePath);
            switch (imagePath.Substring(imagePath.Length - 3) ?? "")
            {
                case "jpg":
                {
                    return new JpegBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default).Frames[0];
                }

                case "bmp":
                {
                    return new BmpBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default).Frames[0];
                }

                case "png":
                {
                    return new PngBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default).Frames[0];
                }

                case "ico":
                {
                    return new IconBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default).Frames[0];
                }

                default:
                {
                    return null;
                }
            }
        }
    }
}