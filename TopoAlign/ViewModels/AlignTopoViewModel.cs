using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace TopoAlign.ViewModels;
internal partial class AlignTopoViewModel : BaseViewModel
{
    public string WindowTitle { get; private set; }

    [ObservableProperty]
    private System.Windows.Visibility _isWindowVisible = System.Windows.Visibility.Visible;

    public AlignTopoViewModel()
    {
        var informationVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;

        WindowTitle = $"TopoAlign {informationVersion}";
    }
}
