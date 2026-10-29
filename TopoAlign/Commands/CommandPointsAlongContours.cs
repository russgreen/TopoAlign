using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Serilog;
using TopoAlign.Geometry;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CommandPointsAlongContours : IExternalCommand
{
    private static readonly ILogger log = Log.ForContext<CommandPointsAlongContours>();

    private UIApplication _uiapp;
    private UIDocument _uidoc;
    private Autodesk.Revit.ApplicationServices.Application _app;
    private Document _doc;
    private Selection _sel;
    private decimal _offset;
    private decimal _divide;
    //private Element _element;
    //private Edge _edge;
    //private View3D _v3d;

#if REVIT2024_OR_GREATER
    private Toposolid _topoSolid;
#else
    private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
#endif

    public Models.Settings cSettings;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        log.Information("{command}", nameof(CommandPointsAlongContours));

        cSettings = new Models.Settings();
        cSettings.LoadSettings();
        _uiapp = commandData.Application;
        _uidoc = _uiapp.ActiveUIDocument;
        _app = _uiapp.Application;
        _doc = _uidoc.Document;
        _sel = _uidoc.Selection;

        //check entitlement
        //if (CheckEntitlement.LicenseCheck(_app) == false)
        //{
        //    return Result.Cancelled;
        //}

        var viewModel = new DivideLinesViewModel(_doc, cSettings.DivideEdgeDistance);
        var view = new DivideLinesView(viewModel);

        if (view.ShowDialog() == true)
        {
            _divide = viewModel.InternalDivide;
            _offset = viewModel.InternalOffset;

            //first save the settings for next time
            cSettings.DivideEdgeDistance = _divide;
            cSettings.SaveSettings();

#if REVIT2024_OR_GREATER
            if (PointsUtils.PointsAlongLines(_uidoc, _doc, _topoSolid, (double)_divide, (double)_offset) == false)
            {
                return Result.Failed;
            }
#else
            if (PointsUtils.PointsAlongLines(_uidoc, _doc, _topoSurface, (double)_divide, (double)_offset) == false)
            {
                return Result.Failed;
            }
#endif
        }

        // Return Success
        return Result.Succeeded;
    }

}
