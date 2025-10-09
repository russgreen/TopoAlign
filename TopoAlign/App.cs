using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace TopoAlign;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
class App : IExternalApplication
{
    public static UIControlledApplication CachedUiCtrApp;
    public static UIApplication CachedUiApp;

    public static Autodesk.Revit.DB.Document RevitDocument;

    //public static Autodesk.Revit.DB.Document revitDocument;
    private static readonly string _domain = Environment.UserDomainName;
    private string _tabName = "Topo Align";

    public Result OnStartup(UIControlledApplication a)
    {
        CachedUiCtrApp = a;

        _ = CreateRibbonPanel();

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a)
    {
        return Result.Succeeded;
    }

    private RibbonPanel CreateRibbonPanel()
    {
        RibbonPanel panel;

        CachedUiCtrApp.CreateRibbonTab(_tabName);
        panel = CachedUiCtrApp.CreateRibbonPanel(_tabName, Guid.NewGuid().ToString());
        panel.Name = "ARBG_TopoAlign_ExtApp";
        panel.Title = "Topo Align";

        PushButton pbTopoAlign = (PushButton)panel.AddItem(new PushButtonData(
        nameof(Commands.CommandAlignTopo),
        $"Align to{Environment.NewLine}Element",
        Assembly.GetExecutingAssembly().Location,
        $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandAlignTopo)}"));
        pbTopoAlign.ToolTip = "Adjust topo to edge or floor geometry";
        pbTopoAlign.LargeImage = PngImageSource("TopoAlign.Images.TopoAlign32.png");

        PushButton pbFloorAlign = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommndAlignFloor),
            $"Align to{Environment.NewLine}Topo",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommndAlignFloor)}"));
#if REVIT2026_OR_GREATER
        pbFloorAlign.ToolTip = "Delete the floor and place a toposolid sub-region";
#else
        pbFloorAlign.ToolTip = "Adjust a floor to follow the topography";
#endif
        pbFloorAlign.LargeImage = PngImageSource("TopoAlign.Images.FloorToTopo32.png");

        PushButton pbPointsFromLines = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandPointsOnSurface),
            $"Points from{Environment.NewLine}Lines",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandPointsOnSurface)}"));
        pbPointsFromLines.ToolTip = "Add points on the surface along selected model lines. Model lines must be lines and arcs and be placed BELOW the topo surface.";
        pbPointsFromLines.LargeImage = PngImageSource("TopoAlign.Images.PointsFromLines32.png");

        PushButton pbPointsAlongContours = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandPointsAlongContours),
            $"Points along{Environment.NewLine}Contours",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandPointsAlongContours)}"));
        pbPointsAlongContours.ToolTip = "Add points on surface along selected contour model lines. Model lines can be placed on zero datum BELOW the topo surface and projected up a set distance using an offset value.";
        pbPointsAlongContours.LargeImage = PngImageSource("TopoAlign.Images.PointsFromContours32.png");

        PushButton pbPointsAtIntersection = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandPointsAtIntersection),
            $"Points at{Environment.NewLine}Intersection",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandPointsAtIntersection)}"));
        pbPointsAtIntersection.ToolTip = "Add points on the surface at the intersection with a selected face.";
        pbPointsAtIntersection.LargeImage = PngImageSource("TopoAlign.Images.TopoAlignPlane32.png");

        PushButton pbResetRegion = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandResetTopoRegion),
            $"Reset{Environment.NewLine}region",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandResetTopoRegion)}"));
        pbResetRegion.ToolTip = "Copy points from existing topo surface to new topo surface within a region to undo changes made.";
        pbResetRegion.LargeImage = PngImageSource("TopoAlign.Images.Reset32.png");

        //set help document
        var contextHelp = new ContextualHelp(ContextualHelpType.Url, @"C:\ProgramData\Autodesk\ApplicationPlugins\rg tools Topo Align.bundle\Contents\help.html");

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
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedPath);
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
