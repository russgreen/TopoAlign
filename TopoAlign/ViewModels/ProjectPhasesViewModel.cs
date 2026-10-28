using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TopoAlign.ViewModels;
internal partial class ProjectPhasesViewModel : BaseViewModel
{
    public string WindowTitle { get; } = "Project Phases";

    public List<string> Phases { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _sourcePhase;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    private string _targetPhase;

    public ProjectPhasesViewModel(IEnumerable<string> phaseNames)
    {
        Phases = phaseNames.ToList();

        SourcePhase = Phases.FirstOrDefault();
        TargetPhase = Phases.Count > 1 ? Phases[1] : SourcePhase;
    }

    private bool CanOk() => SourcePhase is not null && TargetPhase is not null;

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok()
    {
        this.OnClosingRequest();
    }
}
