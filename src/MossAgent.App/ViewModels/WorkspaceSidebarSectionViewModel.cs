using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSidebarSectionViewModel : ObservableObject
{
    private bool _isProjectsExpanded = true;

    public WorkspaceSidebarSectionViewModel()
    {
        ToggleProjectsCommand = new RelayCommand(() =>
            IsProjectsExpanded = !IsProjectsExpanded);
    }

    public bool IsProjectsExpanded
    {
        get => _isProjectsExpanded;
        private set => SetProperty(ref _isProjectsExpanded, value);
    }

    public IRelayCommand ToggleProjectsCommand { get; }

    public void ExpandProjects() => IsProjectsExpanded = true;
}
