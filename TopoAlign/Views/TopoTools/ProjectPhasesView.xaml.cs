using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.TopoTools;

namespace TopoAlign.Views.TopoTools;
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
