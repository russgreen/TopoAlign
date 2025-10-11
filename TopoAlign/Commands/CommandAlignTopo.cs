using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;
using Serilog.Context;
using Serilog.Core;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
public class CommandAlignTopo : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandAlignTopo>();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandAlignTopo));

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        //check entitlement
        //if (CheckEntitlement.LicenseCheck(App.CachedUiApp.Application) == false)
        //{
        //    return Result.Cancelled;
        //}

        var newView = new Views.AlignTopoView();
        newView.ShowDialog();

        //TODO use messaging to get the values back from the view

        return Result.Succeeded;

    }

}
