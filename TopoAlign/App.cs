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
    private static string _domain = System.Environment.UserDomainName;
    private string _tabName = "RG Tools";
    private bool _useAddinsTab = true;

    public Result OnStartup(UIControlledApplication a)
    {
        if (_domain.ToLower().Contains("ece"))
        {
            _tabName = "ECE Tools";
            _useAddinsTab = false;
        }

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

        // Check if "Tab" already exists and use if its there
        try
        {
            panel = cachedUiCtrApp.CreateRibbonPanel(_tabName, Guid.NewGuid().ToString());
            panel.Name = "ARBG_TopoAlign_ExtApp";
            panel.Title = "Topo Align";
        }
        catch
        {
            var pluginPath = @"C:\ProgramData\Autodesk\ApplicationPlugins";
            if (System.IO.Directory.Exists(pluginPath) == true)
            {
                foreach (var folder in System.IO.Directory.GetDirectories(pluginPath))
                {
                    if(folder.ToLower().Contains("rg") == true & folder.ToLower().Contains("rg tools topo align") == false)
                    {
                        _useAddinsTab = false;
                        break;
                    }                    
                }
            }

            if(_useAddinsTab == false)
            {
                cachedUiCtrApp.CreateRibbonTab(_tabName);
                panel = cachedUiCtrApp.CreateRibbonPanel(_tabName, Guid.NewGuid().ToString());
                panel.Name = "ARBG_TopoAlign_ExtApp";
                panel.Title = "Topo Align";
            }
            else
            {
                panel = cachedUiCtrApp.CreateRibbonPanel("Topo Align");
            }
        }

        PushButton pbTopoAlign = (PushButton)panel.AddItem(new PushButtonData(
            nameof(TopoAlign.Commands.CommandAlignTopo), 
            $"Align to{System.Environment.NewLine}Element",
            Assembly.GetExecutingAssembly().Location, 
            $"{nameof(TopoAlign)}.{nameof(TopoAlign.Commands)}.{nameof(TopoAlign.Commands.CommandAlignTopo)}"));
        pbTopoAlign.ToolTip = "Adjust topo to edge or floor geometry";
        pbTopoAlign.LargeImage = PngImageSource("TopoAlign.Images.TopoAlign32.png");

        PushButton pbFloorAlign = (PushButton)panel.AddItem(new PushButtonData(
            nameof(TopoAlign.Commands.CommndAlignFloor), 
            $"Align to{System.Environment.NewLine}Topo",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(TopoAlign.Commands)}.{nameof(TopoAlign.Commands.CommndAlignFloor)}"));
        pbFloorAlign.ToolTip = "Adjust a floor to follow the topography";
        pbFloorAlign.LargeImage = PngImageSource("TopoAlign.Images.FloorToTopo32.png");

        PushButton pbPointsFromLines = (PushButton)panel.AddItem(new PushButtonData(
            nameof(TopoAlign.Commands.CommandPointsOnSurface), 
            $"Points from{System.Environment.NewLine}Lines",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(TopoAlign.Commands)}.{nameof(TopoAlign.Commands.CommandPointsOnSurface)}"));
        pbPointsFromLines.ToolTip = "Add points on the surface along selected model lines. Model lines must be lines and arcs and be placed BELOW the topo surface.";
        pbPointsFromLines.LargeImage = PngImageSource("TopoAlign.Images.PointsFromLines32.png");

        PushButton pbPointsAlongContours  = (PushButton)panel.AddItem(new PushButtonData(
            nameof(TopoAlign.Commands.CommandPointsAlongContours), 
            $"Points along{System.Environment.NewLine}Contours",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(TopoAlign.Commands)}.{nameof(TopoAlign.Commands.CommandPointsAlongContours)}"));
        pbPointsAlongContours.ToolTip = "Add points on surface along selected contour model lines. Model lines can be placed on zero datum BELOW the topo surface and projected up a set distance using an offset value.";
        pbPointsAlongContours.LargeImage = PngImageSource("TopoAlign.Images.PointsFromContours32.png");

        PushButton pbPointsAtIntersection = (PushButton)panel.AddItem(new PushButtonData(
            nameof(TopoAlign.Commands.CommandPointsAtIntersection), 
            $"Points at{System.Environment.NewLine}Intersection",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(TopoAlign.Commands)}.{nameof(TopoAlign.Commands.CommandPointsAtIntersection)}"));
        pbPointsAtIntersection.ToolTip = "Add points on the surface at the intersection with a selected face.";
        pbPointsAtIntersection.LargeImage = PngImageSource("TopoAlign.Images.TopoAlignPlane32.png");

        PushButton pbResetRegion  = (PushButton)panel.AddItem(new PushButtonData(
            nameof(TopoAlign.Commands.CommandResetTopoRegion), 
            $"Reset{System.Environment.NewLine}region",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(TopoAlign.Commands)}.{nameof(TopoAlign.Commands.CommandResetTopoRegion)}"));
        pbResetRegion.ToolTip = "Copy points from existing topo surface to new topo surface within a region to undo changes made.";
        pbResetRegion.LargeImage = PngImageSource("TopoAlign.Images.Reset32.png");

        //set help document
        ContextualHelp contextHelp = new ContextualHelp(ContextualHelpType.Url, @"C:\ProgramData\Autodesk\ApplicationPlugins\rg tools Topo Align.bundle\Contents\help.html");

        pbTopoAlign.SetContextualHelp(contextHelp);
        pbFloorAlign.SetContextualHelp(contextHelp);
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
