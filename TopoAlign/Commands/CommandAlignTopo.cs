using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
public class CommandAlignTopo : IExternalCommand
{
     public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
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
