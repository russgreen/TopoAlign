using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TopoAlign.ViewModels;
internal partial class DivideLinesViewModel : BaseViewModel
{
    [ObservableProperty]
    private decimal _divide;

    [ObservableProperty]
    private decimal _offset;

    [ObservableProperty]
    private List<Models.DisplayUnitModel> _displayUnits = new();

    [ObservableProperty]
    private Models.DisplayUnitModel _selectedDisplayUnit;

    public string WindowTitle { get; } = "Divide Lines";

    public bool IsDivideEnabled { get; }

    public bool AllowNegativeOffset { get; }

    /// <summary>
    /// Divide distance converted to Revit internal units using the selected display unit
    /// </summary>
    public decimal InternalDivide => ToInternalUnits(Divide);

    /// <summary>
    /// Vertical offset converted to Revit internal units using the selected display unit
    /// </summary>
    public decimal InternalOffset => ToInternalUnits(Offset);

    /// <param name="doc">The document used to determine the default display units</param>
    /// <param name="internalDivide">The initial divide distance in Revit internal units</param>
    /// <param name="isDivideEnabled">False when the command doesn't use the divide distance</param>
    /// <param name="allowNegativeOffset">True when the command accepts a negative vertical offset</param>
    public DivideLinesViewModel(Document doc, decimal internalDivide, bool isDivideEnabled = true, bool allowNegativeOffset = false)
    {
        IsDivideEnabled = isDivideEnabled;
        AllowNegativeOffset = allowNegativeOffset;

#if REVIT2018 || REVIT2019 || REVIT2020
        var docDisplayUnits = doc.GetUnits().GetFormatOptions(UnitType.UT_Length).DisplayUnits;
#else
        var docDisplayUnits = doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();
#endif

        PopulateDisplayUnitList(docDisplayUnits);

        Divide = Decimal.Round(Convert.ToDecimal(UnitUtils.ConvertFromInternalUnits((double)internalDivide, docDisplayUnits)), 2);
        Offset = 0;
    }

#if REVIT2018 || REVIT2019 || REVIT2020
    private void PopulateDisplayUnitList(DisplayUnitType docDisplayUnits)
    {
        foreach (DisplayUnitType displayUnitType in UnitUtils.GetValidDisplayUnits(UnitType.UT_Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel
            {
                DisplayUnit = displayUnitType,
                Label = LabelUtils.GetLabelFor(displayUnitType)
            });
        }

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == docDisplayUnits);
    }
#else
    private void PopulateDisplayUnitList(ForgeTypeId docDisplayUnits)
    {
        foreach (ForgeTypeId displayUnitType in UnitUtils.GetValidUnits(SpecTypeId.Length))
        {
            DisplayUnits.Add(new Models.DisplayUnitModel
            {
                DisplayUnit = displayUnitType,
                Label = LabelUtils.GetLabelForUnit(displayUnitType)
            });
        }

        SelectedDisplayUnit = DisplayUnits.FirstOrDefault(x => x.DisplayUnit == docDisplayUnits);
    }
#endif

    private decimal ToInternalUnits(decimal value)
    {
        return Convert.ToDecimal(UnitUtils.ConvertToInternalUnits((double)value, SelectedDisplayUnit.DisplayUnit));
    }

    [RelayCommand]
    private void Ok()
    {
        this.OnClosingRequest();
    }
}
