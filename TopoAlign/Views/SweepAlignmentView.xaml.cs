using System.Windows;
using TopoAlign.ViewModels;

namespace TopoAlign.Views;

/// <summary>
/// Interaction logic for SweepAlignmentView.xaml
/// </summary>
public partial class SweepAlignmentView : Window
{
    internal SweepAlignmentView(SweepAlignmentViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        viewModel.ClosingRequest += (s, e) => DialogResult = true;
        viewModel.HideRequested += (s, e) => Hide();
        viewModel.EditAssembliesRequested += (s, e) =>
        {
            var editor = viewModel.CreateEditor();
            new AssemblyEditorView(editor) { Owner = this }.ShowDialog();
            viewModel.ApplyEditor(editor);
        };
    }
}
