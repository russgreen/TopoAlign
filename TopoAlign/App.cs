#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//using Microsoft.AppCenter;
//using Microsoft.AppCenter.Crashes;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media.Imaging;
#endregion

namespace TopoAlign;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
class App : IExternalApplication
{
    public static UIControlledApplication cachedUiCtrApp;

    //public static Autodesk.Revit.DB.Document revitDocument;

    public Result OnStartup(UIControlledApplication a)
    {
        //AppCenter.LogLevel = LogLevel.Verbose;
        //System.Windows.Forms.Application.ThreadException += (sender, args) =>
        //{
        //    Crashes.TrackError(args.Exception);
        //};

        //Crashes.ShouldAwaitUserConfirmation = () =>
        //{
        //    // Build your own UI to ask for user consent here. SDK doesn't provide one by default.     
        //    var dialog = new DialogUserConfirmation();
        //    dialog.ShowDialog();
        //    Crashes.NotifyUserConfirmation(dialog.ClickResult);

        //    // Return true if you built a UI for user consent and are waiting for user input on that custom UI, otherwise false.     
        //    return true;
        //};

        //AppCenter.Start("c26c8f38-0aad-44c7-9064-478429495727", typeof(Crashes));

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

        // Check if "Archisoft Tools" already exists and use if its there
        try
        {
            panel = cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString());
            panel.Name = "ARBG_TopoAlign_ExtApp";
            panel.Title = "Topo Align";
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
            }

            if(archisoftPanel == true)
            {
                cachedUiCtrApp.CreateRibbonTab("Archisoft Tools");
                panel = cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString());
                panel.Name = "ARBG_TopoAlign_ExtApp";
                panel.Title = "Topo Align";
            }
            else
            {
                panel = cachedUiCtrApp.CreateRibbonPanel("Topo Align");
            }
        }

        PushButtonData pbDataTopoAlign = new PushButtonData("Align to Element", "Align to Element", Assembly.GetExecutingAssembly().Location, "TopoAlign.cmdAlignTopo");
        PushButton pbTopoAlign  = (PushButton)panel.AddItem(pbDataTopoAlign);
        pbTopoAlign.ToolTip = "Adjust topo to edge or floor geometry";
        pbTopoAlign.LargeImage = PngImageSource("TopoAlign.Images.TopoAlign32.png");

        PushButtonData pbDataPointsFromLines  = new PushButtonData("Points from Lines", "Points from Lines", Assembly.GetExecutingAssembly().Location, "TopoAlign.cmdPointsOnSurface");
        PushButton pbPointsFromLines = (PushButton)panel.AddItem(pbDataPointsFromLines);
        pbPointsFromLines.ToolTip = "Add points on surface along selected model lines. Model lines must be lines and arcs and be BELOW the topo surface.";
        pbPointsFromLines.LargeImage = PngImageSource("TopoAlign.Images.PointsFromLines32.png");

        PushButtonData pbDataPointsAlongContours = new PushButtonData("Points along Contours", "Points along Contours", Assembly.GetExecutingAssembly().Location, "TopoAlign.cmdPointsAlongContours");
        PushButton pbPointsAlongContours  = (PushButton)panel.AddItem(pbDataPointsAlongContours);
        pbPointsAlongContours.ToolTip = "Add points on surface along selected contour model lines. Model lines can on a datum BELOW the topo surface and projected up a set distance using an offset value.";
        pbPointsAlongContours.LargeImage = PngImageSource("TopoAlign.Images.PointsFromContours32.png");

        PushButtonData pbDataPointsAtIntersection = new PushButtonData("Points at Intersection", "Points at Intersection", Assembly.GetExecutingAssembly().Location, "TopoAlign.cmdPointsAtIntersection");
        PushButton pbPointsAtIntersection = (PushButton)panel.AddItem(pbDataPointsAtIntersection);
        pbPointsAtIntersection.ToolTip = "Add points on surface at the intersection with a selected face.";
        pbPointsAtIntersection.LargeImage = PngImageSource("TopoAlign.Images.TopoAlignPlane32.png");

        PushButtonData pbDataResetRegion = new PushButtonData("Reset region", "Reset region", Assembly.GetExecutingAssembly().Location, "TopoAlign.cmdResetTopoRegion");
        PushButton pbResetRegion  = (PushButton)panel.AddItem(pbDataResetRegion);
        pbResetRegion.ToolTip = "Copy points from existing topo surface to new topo surface within a region to undo changes made.";
        pbResetRegion.LargeImage = PngImageSource("TopoAlign.Images.Reset32.png");

        //set help document
        ContextualHelp contextHelp = new ContextualHelp(ContextualHelpType.Url, @"C:\ProgramData\Autodesk\ApplicationPlugins\archisoft tools Topo Align.bundle\Contents\help.html");

        pbTopoAlign.SetContextualHelp(contextHelp);
        pbPointsFromLines.SetContextualHelp(contextHelp);
        pbPointsAlongContours.SetContextualHelp(contextHelp);
        pbPointsAtIntersection.SetContextualHelp(contextHelp);
        pbResetRegion.SetContextualHelp(contextHelp);

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
