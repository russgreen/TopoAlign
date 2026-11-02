using static TopoAlign.Tests.TestKit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TopoAlign.Behaviours;

namespace TopoAlign.Tests;

/// <summary>WPF controls need an STA thread, so these tests build the TextBox on a dedicated one.</summary>
public class TextBoxInputTests : TopoAlignTest
{
    private static bool TypeText(TextBox textBox, string text)
    {
        var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, textBox, text))
        {
            RoutedEvent = UIElement.PreviewTextInputEvent
        };
        textBox.RaiseEvent(args);
        return args.Handled;
    }

    [Test]
    public async Task RegularExpression_CanBeSetAndRead()
    {
        string value = null, unset = null;
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();
            unset = TextBoxInput.GetRegularExpression(textBox);
            TextBoxInput.SetRegularExpression(textBox, "^[0-9]*$");
            value = TextBoxInput.GetRegularExpression(textBox);
        });

        await Assert.That(unset).IsNull();
        await Assert.That(value).IsEqualTo("^[0-9]*$");
    }

    [Test]
    public async Task InputThatMatchesTheExpression_IsAllowed()
    {
        bool digitHandled = true;
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();
            TextBoxInput.SetRegularExpression(textBox, "^[0-9]+$");
            digitHandled = TypeText(textBox, "7");
        });

        await Assert.That(digitHandled).IsFalse();
    }

    [Test]
    public async Task InputThatDoesNotMatchTheExpression_IsBlocked()
    {
        bool letterHandled = false;
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();
            TextBoxInput.SetRegularExpression(textBox, "^[0-9]+$");
            letterHandled = TypeText(textBox, "a");
        });

        await Assert.That(letterHandled).IsTrue();
    }

    [Test]
    public async Task ClearingTheExpression_RemovesTheFilter()
    {
        bool handledAfterClearing = true;
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();
            TextBoxInput.SetRegularExpression(textBox, "^[0-9]+$");
            TextBoxInput.SetRegularExpression(textBox, null);
            handledAfterClearing = TypeText(textBox, "a");
        });

        await Assert.That(handledAfterClearing).IsFalse();
    }

    [Test]
    public async Task ChangingTheExpression_DoesNotStackHandlers()
    {
        bool digitHandled = true, letterHandled = false;
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();
            TextBoxInput.SetRegularExpression(textBox, "^[a-z]+$");
            TextBoxInput.SetRegularExpression(textBox, "^[0-9]+$");
            digitHandled = TypeText(textBox, "5");
            letterHandled = TypeText(textBox, "x");
        });

        await Assert.That(digitHandled).IsFalse();
        await Assert.That(letterHandled).IsTrue();
    }

    [Test]
    public async Task NonTextBoxElements_AreIgnored()
    {
        string value = null;
        RunOnStaThread(() =>
        {
            var button = new Button();
            TextBoxInput.SetRegularExpression(button, "^[0-9]+$");
            value = TextBoxInput.GetRegularExpression(button);
        });

        await Assert.That(value).IsEqualTo("^[0-9]+$");
    }
}
