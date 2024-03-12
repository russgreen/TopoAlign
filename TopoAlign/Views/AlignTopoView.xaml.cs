using System.Windows;

namespace TopoAlign.Views;
/// <summary>
/// Interaction logic for AlignTopoView.xaml
/// </summary>
public partial class AlignTopoView : Window
{
    public AlignTopoView()
    {
        InitializeComponent();

        var _ = new Microsoft.Xaml.Behaviors.DefaultTriggerAttribute(typeof(Trigger), typeof(Microsoft.Xaml.Behaviors.TriggerBase), null);
    }
}
