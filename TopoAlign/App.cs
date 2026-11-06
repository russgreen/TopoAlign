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
    public static UIControlledApplication CachedUiCtrApp = null!;
    public static ControlledApplication CtrApp = null!;
    public static UIApplication CachedUiApp = null!;

    public static Autodesk.Revit.DB.Document RevitDocument = null!;

    //public static Autodesk.Revit.DB.Document revitDocument;
    private static readonly string _domain = Environment.UserDomainName;
    private string _tabName = "Topo Align";

    public Result OnStartup(UIControlledApplication a)
    {
        CachedUiCtrApp = a;
        CtrApp = a.ControlledApplication;

        var cultureInfo = Thread.CurrentThread.CurrentCulture;
        var regionInfo = new RegionInfo(cultureInfo.Name);
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

        PushButton pbAlignTopoEdges = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandAlignTopoEdges),
            $"Align Topo{Environment.NewLine}Edges",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandAlignTopoEdges)}"));
        pbAlignTopoEdges.ToolTip = "Add points to two topos that share an edge so that the edges line up.";
        pbAlignTopoEdges.LargeImage = PngImageSource("TopoAlign.Images.AlignTopoEdges32.png");
        pbAlignTopoEdges.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandAlignTopoEdges.html"));

        PushButton pbPointsFromLines =(PushButton)panel.AddItem(new PushButtonData(
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

        PushButton pbCreateAlignment = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandCreateAlignment),
            $"Create{Environment.NewLine}Alignment",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandCreateAlignment)}"));
        pbCreateAlignment.ToolTip = "Create a native Revit alignment from model/detail lines and arcs.";
        pbCreateAlignment.LargeImage = PngImageSource("TopoAlign.Images.AlignmentFromCurve32.png");
        pbCreateAlignment.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandCreateAlignment.html"));

        PushButton pbEditAlignment = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandEditAlignment),
            $"Edit{Environment.NewLine}Alignment",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandEditAlignment)}"));
        pbEditAlignment.ToolTip = "Rename an alignment, change its start station or reverse its direction.";
        pbEditAlignment.LargeImage = PngImageSource("TopoAlign.Images.EditAlignment32.png");
        pbEditAlignment.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandEditAlignment.html"));

        PushButton pbEditProfile = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandEditProfile),
            $"Edit{Environment.NewLine}Profile",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandEditProfile)}"));
        pbEditProfile.ToolTip = "Add stations to an alignment's profile and set the elevation of each.";
        pbEditProfile.LargeImage = PngImageSource("TopoAlign.Images.EditProfile32.png");
        pbEditProfile.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandEditProfile.html"));

        PushButton pbStationLabels = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandStationLabels),
            $"Station{Environment.NewLine}Labels",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandStationLabels)}"));
        pbStationLabels.ToolTip = "Place a set of station labels along an alignment in the active view.";
        pbStationLabels.LargeImage = PngImageSource("TopoAlign.Images.StationLabels32.png");
        pbStationLabels.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandStationLabels.html"));

        PushButton pbSweepAlignment = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandSweepAlignment),
            $"Sweep Along{Environment.NewLine}Alignment",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandSweepAlignment)}"));
        pbSweepAlignment.ToolTip = "Sweep a profile family along an alignment to make a road or path.";
        pbSweepAlignment.LargeImage = PngImageSource("TopoAlign.Images.SweepAlongAlignment32.png");
        pbSweepAlignment.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandSweepAlignment.html"));

#if REVIT2024_OR_GREATER
        PushButton pbSweepToToposolid = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandSweepToToposolid),
            $"Sweep to{Environment.NewLine}Toposolid",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandSweepToToposolid)}"));
        pbSweepToToposolid.ToolTip = "Convert a road made with Sweep Along Alignment into a toposolid that follows its surface.";
        pbSweepToToposolid.LargeImage = PngImageSource("TopoAlign.Images.SweepToToposolid32.png");
        pbSweepToToposolid.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandSweepToToposolid.html"));

        PushButton pbGradeToSurface = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandGradeToSurface),
            $"Grade to{Environment.NewLine}Surface",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandGradeToSurface)}"));
        pbGradeToSurface.ToolTip = "Grade a slope from an edge or model line down or up to the toposolid, replacing the toposolid points in between.";
        pbGradeToSurface.LargeImage = PngImageSource("TopoAlign.Images.GradeToSurface32.png");
        pbGradeToSurface.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandGradeToSurface.html"));

        PushButton pbDroppedKerbRamp = (PushButton)panel.AddItem(new PushButtonData(
            nameof(Commands.CommandDroppedKerbRamp),
            $"Dropped{Environment.NewLine}Kerb Ramp",
            Assembly.GetExecutingAssembly().Location,
            $"{nameof(TopoAlign)}.{nameof(Commands)}.{nameof(Commands.CommandDroppedKerbRamp)}"));
        pbDroppedKerbRamp.ToolTip = "Shape a toposolid edge into a dropped kerb: a ramp down to the level of a target edge, with a flare either side.";
        pbDroppedKerbRamp.LargeImage = PngImageSource("TopoAlign.Images.DroppedKerbRamp32.png");
        pbDroppedKerbRamp.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, @"https://russgreen.github.io/TopoAlign/Commands/CommandDroppedKerbRamp.html"));
#endif

        return panel;
    }

    private System.Windows.Media.ImageSource? PngImageSource(string embeddedPath)
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedPath);
        System.Windows.Media.ImageSource? imageSource;
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
