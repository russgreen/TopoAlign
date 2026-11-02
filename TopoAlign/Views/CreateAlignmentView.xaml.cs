using System.Windows;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;

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
