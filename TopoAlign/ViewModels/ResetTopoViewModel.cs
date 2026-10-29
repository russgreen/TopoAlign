using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TopoAlign.ViewModels;
internal partial class ResetTopoViewModel : BaseViewModel
{
    public string WindowTitle { get; } = "Reset Topo";

    public List<Models.TopoItemModel> SourceTopos { get; }

    public List<Models.TopoItemModel> TargetTopos { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private Models.TopoItemModel _selectedSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private Models.TopoItemModel _selectedTarget;

    public ResetTopoViewModel(IEnumerable<Models.TopoItemModel> sourceTopos, IEnumerable<Models.TopoItemModel> targetTopos)
    {
        SourceTopos = sourceTopos.ToList();
        TargetTopos = targetTopos.ToList();

        SelectedSource = SourceTopos.FirstOrDefault();
        SelectedTarget = TargetTopos.FirstOrDefault();
    }

    private bool CanOk() => SelectedSource is not null && SelectedTarget is not null;

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok()
    {
        this.OnClosingRequest();
    }
}
