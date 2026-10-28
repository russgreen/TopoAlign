using System.Windows;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;
/// <summary>
/// Interaction logic for ResetTopoView.xaml
/// </summary>
public partial class ResetTopoView : Window
{
    internal ResetTopoView(ResetTopoViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
    }
}
