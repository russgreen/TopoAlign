using System.Windows;
using TopoAlign.ViewModels.AlignmentTools;

namespace TopoAlign.Views.AlignmentTools;

/// <summary>
/// Interaction logic for TJunctionView.xaml
/// </summary>
public partial class TJunctionView : Window
{
    internal TJunctionView(TJunctionViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
