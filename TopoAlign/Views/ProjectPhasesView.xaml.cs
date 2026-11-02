using System.Windows;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;
/// <summary>
/// Interaction logic for ProjectPhasesView.xaml
/// </summary>
public partial class ProjectPhasesView : Window
{
    internal ProjectPhasesView(ProjectPhasesViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
    }
}
