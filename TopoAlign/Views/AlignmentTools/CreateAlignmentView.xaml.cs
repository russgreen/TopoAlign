using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.AlignmentTools;

namespace TopoAlign.Views.AlignmentTools;

/// <summary>
/// Interaction logic for CreateAlignmentView.xaml
/// </summary>
public partial class CreateAlignmentView : Window
{
    internal CreateAlignmentView(CreateAlignmentViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
