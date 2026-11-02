#if REVIT2024_OR_GREATER
using System.Windows;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;

/// <summary>
/// Interaction logic for SweepToToposolidView.xaml
/// </summary>
public partial class SweepToToposolidView : Window
{
    internal SweepToToposolidView(SweepToToposolidViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
#endif
