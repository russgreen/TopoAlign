using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.AlignmentTools;

namespace TopoAlign.Views.AlignmentTools;

/// <summary>
/// Interaction logic for EditAlignmentView.xaml
/// </summary>
public partial class EditAlignmentView : Window
{
    internal EditAlignmentView(EditAlignmentViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
