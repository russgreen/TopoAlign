using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace TopoAlign.Behaviours;

/// <summary>
/// Value converter for numeric TextBoxes bound with UpdateSourceTrigger=PropertyChanged.
/// Stops the binding rewriting the text while typing, which would otherwise strip a trailing
/// decimal point ("50." becomes "50") and make it impossible to type decimals.
/// Use as a markup extension so every binding gets its own instance:
/// Converter="{behaviours:NumericText}".
/// </summary>
[MarkupExtensionReturnType(typeof(IValueConverter))]
internal sealed class NumericTextExtension : MarkupExtension, IValueConverter
{
    private bool _hasTyped;
    private decimal _lastTyped;

    public override object ProvideValue(IServiceProvider serviceProvider) => new NumericTextExtension();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        // Source changed to the value the user has just typed: keep their text as is.
        if (_hasTyped && TryToDecimal(value, out var d) && d == _lastTyped)
        {
            return Binding.DoNothing;
        }

        return System.Convert.ToString(value, culture) ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim() ?? string.Empty;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (!decimal.TryParse(text, NumberStyles.Float, culture, out var d) &&
            !decimal.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
        {
            // "", "-", "." etc. while typing: leave the source untouched.
            _hasTyped = false;
            return Binding.DoNothing;
        }

        _hasTyped = true;
        _lastTyped = d;

        try
        {
            return System.Convert.ChangeType(d, type, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return Binding.DoNothing;
        }
    }

    private static bool TryToDecimal(object value, out decimal d)
    {
        try
        {
            d = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception)
        {
            d = 0;
            return false;
        }
    }
}
