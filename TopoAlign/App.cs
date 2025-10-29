using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Serilog;
using Serilog.Sinks.GoogleAnalytics;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Media.Imaging;
using TopoAlign.Helpers;

namespace TopoAlign;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
class App : IExternalApplication
{
    public static UIControlledApplication CachedUiCtrApp;
    public static ControlledApplication CtrApp;
    public static UIApplication CachedUiApp;

    public static Autodesk.Revit.DB.Document RevitDocument;

    //public static Autodesk.Revit.DB.Document revitDocument;
    private static readonly string _domain = Environment.UserDomainName;
    private string _tabName = "Topo Align";

    public Result OnStartup(UIControlledApplication a)
    {
        CachedUiCtrApp = a;
        CtrApp = a.ControlledApplication;

        var cultureInfo = Thread.CurrentThread.CurrentCulture;
        var regionInfo = new RegionInfo(cultureInfo.LCID);
        var clientId = ClientIdProvider.GetOrCreateClientId();

        var loggerConfigTopoAlign = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug();

#if RELEASE
        loggerConfigTopoAlign = loggerConfigTopoAlign
                .WriteTo.GoogleAnalytics(opts =>
                {
                    opts.MeasurementId = "##MEASUREMENTID##";
                    opts.ApiSecret = "##APISECRET##";
                    opts.ClientId = clientId;

                    opts.FlushPeriod = TimeSpan.FromSeconds(1);
                    opts.BatchSizeLimit = 1;
                    opts.MaxEventsPerRequest = 1;
                    //opts.IncludePredicate = e => e.Properties.ContainsKey("UsageTracking");

                    opts.GlobalParams["app_version"] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                    opts.GlobalParams["revit_version"] = CtrApp.VersionNumber;

                    opts.CountryId = regionInfo.TwoLetterISORegionName;
                });
#endif

        Log.Logger = loggerConfigTopoAlign.CreateLogger();

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
        pbTopoAlign.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandAlignTopo.html"));

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
        pbFloorAlign.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandAlignFloor.html"));

        PushButton pbPointsFromLines = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandPointsOnSurface),
            $"Points from{Environment.NewLine}Lines",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandPointsOnSurface)}"));
        pbPointsFromLines.ToolTip = "Add points on the surface along selected model lines. Model lines must be lines and arcs and be placed BELOW the topo surface.";
        pbPointsFromLines.LargeImage = PngImageSource("TopoAlign.Images.PointsFromLines32.png");
        pbPointsFromLines.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandPointsOnSurface.html"));

        PushButton pbPointsAlongContours = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandPointsAlongContours),
            $"Points along{Environment.NewLine}Contours",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandPointsAlongContours)}"));
        pbPointsAlongContours.ToolTip = "Add points on surface along selected contour model lines. Model lines can be placed on zero datum BELOW the topo surface and projected up a set distance using an offset value.";
        pbPointsAlongContours.LargeImage = PngImageSource("TopoAlign.Images.PointsFromContours32.png");
        pbPointsAlongContours.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandPointsAlongContours.html"));

        PushButton pbPointsAtIntersection = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandPointsAtIntersection),
            $"Points at{Environment.NewLine}Intersection",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandPointsAtIntersection)}"));
        pbPointsAtIntersection.ToolTip = "Add points on the surface at the intersection with a selected face.";
        pbPointsAtIntersection.LargeImage = PngImageSource("TopoAlign.Images.TopoAlignPlane32.png");
        pbPointsAtIntersection.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandPointsAtIntersection.html"));

        PushButton pbResetRegion = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandResetTopoRegion),
            $"Reset{Environment.NewLine}region",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandResetTopoRegion)}"));
        pbResetRegion.ToolTip = "Copy points from existing topo surface to new topo surface within a region to undo changes made.";
        pbResetRegion.LargeImage = PngImageSource("TopoAlign.Images.Reset32.png");
        pbResetRegion.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandResetTopoRegion.html"));

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
