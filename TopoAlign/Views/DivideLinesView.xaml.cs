using System.Windows;
using TopoAlign.Behaviours;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;
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
