using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
public class CommandAlignTopo : IExternalCommand
{
    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app;
    private Document _doc;
    private Selection _sel;
    private decimal _offset;
    private decimal _divide;
    private Element _element;
    private Edge _edge;

#if REVIT2024_OR_GREATER
    private Toposolid _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
#endif

    private Units _docUnits;

#if REVIT2018 || REVIT2019 || REVIT2020
    private DisplayUnitType _docDisplayUnits; 
    private DisplayUnitType _useDisplayUnits;
#else
    private ForgeTypeId _docDisplayUnits;
    private ForgeTypeId _useDisplayUnits;
#endif

    public Models.Settings cSettings;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        cSettings = new Models.Settings();
        cSettings.LoadSettings();

        _uiapp = commandData.Application;
        _uidoc = _uiapp.ActiveUIDocument;
        _app = _uiapp.Application;
        _doc = _uidoc.Document;
        _sel = _uidoc.Selection;

        //check entitlement
        if (CheckEntitlement.LicenseCheck(_app) == false)
        {
            return Result.Cancelled;
        }

        App.CachedUiApp = commandData.Application;
        App.RevitDocument = commandData.Application.ActiveUIDocument.Document;

        var newView = new Views.AlignTopoView();
        newView.ShowDialog();

        //TODO use messaging to get the values back from the view

        return Result.Succeeded;

    }

}
