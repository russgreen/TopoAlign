using System.Windows;
using System.Windows.Input;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;

/// <summary>
/// Modeless progress window. It is created and shown by <see cref="Services.ProgressService"/> on its own
/// UI thread so it keeps repainting while Revit is busy.
/// </summary>
public partial class ProgressWindow : Window
{
    internal ProgressWindow(ProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
        {
            DragMove();
        }
    }
}
