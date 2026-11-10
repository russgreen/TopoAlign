using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.AlignmentTools;

namespace TopoAlign.Views.AlignmentTools;

/// <summary>
/// Interaction logic for EditProfileView.xaml
/// </summary>
public partial class EditProfileView : Window
{
    internal EditProfileView(EditProfileViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
