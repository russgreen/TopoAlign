using Microsoft.Xaml.Behaviors;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace TopoAlign.Behaviours;
internal class AllowableCharactersTextBoxBehavior : Behavior<System.Windows.Controls.TextBox>
{
    public static readonly DependencyProperty RegularExpressionProperty =
             DependencyProperty.Register("RegularExpression", typeof(string), typeof(AllowableCharactersTextBoxBehavior),
             new FrameworkPropertyMetadata(".*"));
    public string RegularExpression
    {
        get
        {
            return (string)base.GetValue(RegularExpressionProperty);
        }
        set
        {
            base.SetValue(RegularExpressionProperty, value);
        }
    }

    public static readonly DependencyProperty MaxLengthProperty =
        DependencyProperty.Register("MaxLength", typeof(int), typeof(AllowableCharactersTextBoxBehavior),
        new FrameworkPropertyMetadata(int.MinValue));

    public int MaxLength
    {
        get
        {
            return (int)base.GetValue(MaxLengthProperty);
        }
        set
        {
            base.SetValue(MaxLengthProperty, value);
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        System.Windows.DataObject.AddPastingHandler(AssociatedObject, OnPaste);
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            string text = Convert.ToString(e.DataObject.GetData(System.Windows.DataFormats.Text));

            if (!IsValid(text, true))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !IsValid(e.Text, false);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        System.Windows.DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
    }

    private bool IsValid(string newText, bool paste)
    {
        var lengthOK = !ExceedsMaxLength(newText, paste);
        var regexMatch = Regex.IsMatch(newText, RegularExpression);
        return lengthOK && regexMatch;

        //return !ExceedsMaxLength(newText, paste) && Regex.IsMatch(String.Concat(this.AssociatedObject.Text, newText), RegularExpression); 
    }

    private bool ExceedsMaxLength(string newText, bool paste)
    {
        if (MaxLength == 0) return false;

        return LengthOfModifiedText(newText, paste) > MaxLength;
    }

    private int LengthOfModifiedText(string newText, bool paste)
    {
        var countOfSelectedChars = this.AssociatedObject.SelectedText.Length;
        var caretIndex = this.AssociatedObject.CaretIndex;
        string text = this.AssociatedObject.Text;

        if (countOfSelectedChars > 0 || paste)
        {
            text = text.Remove(caretIndex, countOfSelectedChars);
            return text.Length + newText.Length;
        }
        else
        {
            var insert = Keyboard.IsKeyToggled(Key.Insert);

            return insert && caretIndex < text.Length ? text.Length : text.Length + newText.Length;
        }
    }


}

