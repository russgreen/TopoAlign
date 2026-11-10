#if REVIT2024_OR_GREATER
using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.RoadTools;

namespace TopoAlign.Views.RoadTools;

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
