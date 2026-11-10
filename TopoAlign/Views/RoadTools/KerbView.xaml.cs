using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.RoadTools;

namespace TopoAlign.Views.RoadTools;

/// <summary>
/// Interaction logic for KerbView.xaml
/// </summary>
public partial class KerbView : Window
{
    internal KerbView(KerbViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
