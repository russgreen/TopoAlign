using System.Windows;
using TopoAlign.Behaviours;
using TopoAlign.ViewModels;
using TopoAlign.ViewModels.TopoTools;

namespace TopoAlign.Views.TopoTools;
/// <summary>
/// Interaction logic for DivideLinesView.xaml
/// </summary>
public partial class DivideLinesView : Window
{
    internal DivideLinesView(DivideLinesViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;

        if (viewModel.AllowNegativeOffset)
        {
            TextBoxInput.SetRegularExpression(OffsetTextBox, "^-?[0-9]*[.,]?[0-9]*$");
        }
    }
}
