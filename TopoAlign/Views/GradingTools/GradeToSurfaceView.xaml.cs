#if REVIT2024_OR_GREATER
using System.Windows;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.GradingTools;

namespace TopoAlign.Views.GradingTools;

/// <summary>
/// Interaction logic for GradeToSurfaceView.xaml
/// </summary>
public partial class GradeToSurfaceView : Window
{
    internal GradeToSurfaceView(GradeToSurfaceViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
    }
}
#endif
