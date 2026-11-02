using static TopoAlign.Tests.TestKit;
using Autodesk.Revit.DB;
using System.Reflection;
using System.Windows;
using Visibility = System.Windows.Visibility;
using TopoAlign.Behaviours;
using TopoAlign.ViewModels;
using TopoAlign.Views;

namespace TopoAlign.Tests;

/// <summary>
/// Builds the WPF windows on a dedicated STA thread. This proves each XAML file still loads and that the
/// code-behind wires up its view model. The view models are created on the Revit thread first because they
/// read from Revit documents; they are not tied to a thread, so the windows can use them afterwards.
/// </summary>
public class ViewTests : TopoAlignTest
{
    [Test]
    public async Task ProjectPhasesView_BindsItsViewModel()
    {
        var viewModel = new ProjectPhasesViewModel(new[] { "Existing", "New" });
        object context = null;
        string title = null;

        RunOnStaThread(() =>
        {
            var view = new ProjectPhasesView(viewModel);
            context = view.DataContext;
            title = view.Title;
        });

        await Assert.That(context).IsEqualTo(viewModel);
        await Assert.That(title).IsNotNull();
    }

    [Test]
    public async Task ResetTopoView_BindsItsViewModel()
    {
        var viewModel = new ResetTopoViewModel(new List<Models.TopoItemModel>(), new List<Models.TopoItemModel>());
        object context = null;

        RunOnStaThread(() => context = new ResetTopoView(viewModel).DataContext);

        await Assert.That(context).IsEqualTo(viewModel);
    }

    [Test]
    public async Task Views_ClosingRequestOutsideADialog_CannotSetDialogResult()
    {
        // The windows close themselves by setting DialogResult, which only works while shown with ShowDialog.
        var viewModel = new ProjectPhasesViewModel(new[] { "A", "B" });
        Exception failure = null;

        RunOnStaThread(() =>
        {
            _ = new ProjectPhasesView(viewModel);
            failure = Catch(() => viewModel.OkCommand.Execute(null));
        });

        await Assert.That(failure is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task DivideLinesView_RestrictsOffsetInputOnlyWhenNegativeOffsetsAreAllowed()
    {
        var document = NewDocument();
        var positiveOnly = new DivideLinesViewModel(document, 1m);
        var allowNegative = new DivideLinesViewModel(document, 1m, allowNegativeOffset: true);
        string defaultExpression = "unset", negativeExpression = "unset";

        RunOnStaThread(() =>
        {
            defaultExpression = TextBoxInput.GetRegularExpression(new DivideLinesView(positiveOnly).OffsetTextBox);
            negativeExpression = TextBoxInput.GetRegularExpression(new DivideLinesView(allowNegative).OffsetTextBox);
        });

        await Assert.That(defaultExpression).IsEqualTo(@"^[0-9]*\.?[0-9]*$");
        await Assert.That(negativeExpression).IsEqualTo("^-?[0-9]*[.,]?[0-9]*$");
    }


    // ---- progress window ----------------------------------------------------------------------------------------

    [Test]
    public async Task ProgressWindow_CountsStepsAndFormatsTheLabel()
    {
        double value = -1, maximum = -1;
        string caption = null, label = null, updatedCaption = null;
        bool aborted = true;

        RunOnStaThread(() =>
        {
            using var window = new ProgressWindow("Working", "{0} done", 5);
            caption = window.CaptionLanel.Text;
            window.Increment();
            window.Increment();
            value = window.Progress.Value;
            maximum = window.Progress.Maximum;
            label = window.ProgressLabel.Text;
            window.SetCaption("Still working");
            updatedCaption = window.CaptionLanel.Text;
            aborted = window.GetAbortFlag();
        });

        await Assert.That(caption).IsEqualTo("Working");
        await Assert.That(value).IsEqualTo(2.0);
        await Assert.That(maximum).IsEqualTo(5.0);
        await Assert.That(label).IsEqualTo("2 done");
        await Assert.That(updatedCaption).IsEqualTo("Still working");
        await Assert.That(aborted).IsFalse();
    }

    [Test]
    public async Task ProgressWindow_WithoutAFormat_LeavesTheLabelEmpty()
    {
        string label = "unset";

        RunOnStaThread(() =>
        {
            using var window = new ProgressWindow("Working", null, 3);
            window.Increment();
            label = window.ProgressLabel.Text;
        });

        await Assert.That(label).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ProgressWindow_Cancel_SetsTheAbortFlag()
    {
        bool before = true, after = false;
        object content = null;

        RunOnStaThread(() =>
        {
            using var window = new ProgressWindow("Working", "{0}", 3);
            before = window.GetAbortFlag();
            typeof(ProgressWindow)
                .GetMethod("ButtonCancel_Click", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { window.AbortButton, new RoutedEventArgs() });
            after = window.GetAbortFlag();
            content = window.AbortButton.Content;
        });

        await Assert.That(before).IsFalse();
        await Assert.That(after).IsTrue();
        await Assert.That(content).IsEqualTo("Cancelling...");
    }

    [Test]
    public async Task ProgressWindow_DefaultConstructor_Loads()
    {
        bool loaded = false;

        RunOnStaThread(() =>
        {
            var window = new ProgressWindow();
            loaded = window is not null;
        });

        await Assert.That(loaded).IsTrue();
    }
}

/// <summary>Needs App (a RevitAPIUI type), so it runs through the UI executor.</summary>
public class CreateAlignmentViewTests : TopoAlignUiTest
{
    [Test]
    public async Task CreateAlignmentView_HidesItselfWhenAPickIsRequested()
    {
        var previous = App.RevitDocument;
        try
        {
            using var backup = new RegistryBackup();
            App.RevitDocument = NewDocument();
            var viewModel = new CreateAlignmentViewModel();
            Visibility visibility = Visibility.Visible;
            object context = null;

            RunOnStaThread(() =>
            {
                var view = new CreateAlignmentView(viewModel);
                context = view.DataContext;
                viewModel.PickLinesCommand.Execute(null);
                visibility = view.Visibility;
            });

            await Assert.That(context).IsEqualTo(viewModel);
            await Assert.That(viewModel.PickRequested).IsTrue();
            await Assert.That(visibility).IsEqualTo(Visibility.Hidden);
        }
        finally
        {
            App.RevitDocument = previous;
        }
    }
}
