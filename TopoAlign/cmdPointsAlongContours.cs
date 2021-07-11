using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class cmdPointsAlongContours : IExternalCommand
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
        private Autodesk.Revit.DB.Architecture.TopographySurface _topoSurface;
        private View3D _v3d;
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

#if REVIT2018 || REVIT2019 || REVIT2020
            _docUnits = _doc.GetUnits();
            _docDisplayUnits = _doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
            _docUnits = _doc.GetUnits();
            _docDisplayUnits = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif
            using (FormDivideLines frm = new FormDivideLines())
            {
                _divide = Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)cSettings.DivideEdgeDistance, _docDisplayUnits));

                frm.nudVertOffset.Value = 0;

                if (_divide > frm.nudDivide.Maximum)
                {
                    frm.nudDivide.Value = frm.nudDivide.Maximum;
                }
                else
                {
                    frm.nudDivide.Value = _divide;
                }

#if REVIT2018 || REVIT2019 || REVIT2020
                foreach (DisplayUnitType displayUnitType in UnitUtils.GetValidDisplayUnits(UnitType.UT_Length))
                {
                    frm.DisplayUnitTypecomboBox.Items.AddRange(new object[] { displayUnitType });
                    frm.DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelFor(displayUnitType));
                }
#else
                foreach (ForgeTypeId displayUnitType in UnitUtils.GetValidUnits(SpecTypeId.Length))
                {
                    frm.DisplayUnitTypecomboBox.Items.AddRange(new object[] { displayUnitType });
                    frm.DisplayUnitcomboBox.Items.Add(LabelUtils.GetLabelForUnit(displayUnitType));
                }
#endif
                frm.DisplayUnitTypecomboBox.SelectedItem = _docDisplayUnits;
                frm.DisplayUnitcomboBox.SelectedIndex = frm.DisplayUnitTypecomboBox.SelectedIndex;

                if (frm.ShowDialog() == DialogResult.OK)
                {
#if REVIT2018 || REVIT2019 || REVIT2020
                    _useDisplayUnits = (DisplayUnitType)frm.DisplayUnitTypecomboBox.SelectedItem;
                    _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
                    _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#else
                    _useDisplayUnits = (ForgeTypeId)frm.DisplayUnitTypecomboBox.SelectedItem;
                    _divide = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudDivide.Value, _useDisplayUnits));
                    _offset = Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)frm.nudVertOffset.Value, _useDisplayUnits));
#endif

                    //first save the settings for next time
                    cSettings.DivideEdgeDistance = _divide;
                    cSettings.SaveSettings();

                    if (PointsUtils.PointsAlongLines(_uidoc, _doc, _topoSurface, (double)_divide, (double)_offset) == false)
                    {
                        return Result.Failed;
                    }
                }
            }

            // Return Success
            return Result.Succeeded;
        }

    }
}
