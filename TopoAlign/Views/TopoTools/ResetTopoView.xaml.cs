using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.TopoTools;

namespace TopoAlign.Views.TopoTools;
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
