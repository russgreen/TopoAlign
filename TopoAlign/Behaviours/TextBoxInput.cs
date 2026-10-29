using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace TopoAlign.Behaviours;

/// <summary>
/// Restricts the text typed or pasted into a TextBox to input matching a regular expression.
/// Implemented as an attached property so there is no dependency on Microsoft.Xaml.Behaviors,
/// which can clash with the copy loaded by Revit.
/// </summary>
internal static class TextBoxInput
{
    public static readonly DependencyProperty RegularExpressionProperty =
        DependencyProperty.RegisterAttached("RegularExpression", typeof(string), typeof(TextBoxInput),
            new PropertyMetadata(null, OnRegularExpressionChanged));

    public static string GetRegularExpression(DependencyObject obj) => (string)obj.GetValue(RegularExpressionProperty);

    public static void SetRegularExpression(DependencyObject obj, string value) => obj.SetValue(RegularExpressionProperty, value);

    private static void OnRegularExpressionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(textBox, OnPaste);

        if (e.NewValue is string)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPaste);
        }
    }

    private static void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !IsValid((TextBox)sender, e.Text);
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var text = Convert.ToString(e.DataObject.GetData(DataFormats.Text));

            if (!IsValid((TextBox)sender, text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static bool IsValid(TextBox textBox, string newText)
    {
        return Regex.IsMatch(newText, GetRegularExpression(textBox));
    }
}
