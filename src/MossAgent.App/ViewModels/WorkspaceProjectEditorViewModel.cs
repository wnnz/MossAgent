using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectEditorViewModel : ObservableObject
{
    private readonly WorkspaceProjectService _projectService;
    private string _name = string.Empty;
    private string _status = string.Empty;
    private bool _isOpen;

    public WorkspaceProjectEditorViewModel(WorkspaceProjectService projectService)
    {
        _projectService = projectService;
        AddCommand = new AsyncRelayCommand(AddAsync);
        AddDirectoryCommand = new AsyncRelayCommand(AddDirectoryAsync);
    }

    public event Action<ProjectProfile>? ProjectCreated;

    public IAsyncRelayCommand AddCommand { get; }

    public IAsyncRelayCommand AddDirectoryCommand { get; }

    public ObservableCollection<WorkspaceProjectDirectoryViewModel> Directories { get; } = [];

    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }

    private async Task AddAsync()
    {
        try
        {
            var project = await _projectService.CreateAsync(
                Name, Directories.Select(static directory => directory.Path).ToArray());
            ProjectCreated?.Invoke(project);
            Name = string.Empty;
            Directories.Clear();
            Status = "项目已添加。";
            IsOpen = false;
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private async Task AddDirectoryAsync()
    {
        var selection = await _projectService.PickAsync(Name);
        if (selection is null)
        {
            return;
        }

        if (Directories.Any(directory => string.Equals(
                directory.Path, selection.Value.Path, StringComparison.OrdinalIgnoreCase)))
        {
            Status = "该目录已经添加。";
            return;
        }

        Directories.Add(new WorkspaceProjectDirectoryViewModel(
            selection.Value.Path, Directories.Count == 0, SetPrimary, Remove));
        if (string.IsNullOrWhiteSpace(Name)) Name = selection.Value.SuggestedName;
        Status = string.Empty;
    }

    private void SetPrimary(WorkspaceProjectDirectoryViewModel selected)
    {
        var index = Directories.IndexOf(selected);
        if (index <= 0) return;
        Directories.Move(index, 0);
        UpdatePrimaryStates();
    }

    private void Remove(WorkspaceProjectDirectoryViewModel selected)
    {
        Directories.Remove(selected);
        UpdatePrimaryStates();
    }

    private void UpdatePrimaryStates()
    {
        for (var index = 0; index < Directories.Count; index++)
        {
            Directories[index].SetPrimaryState(index == 0);
        }
    }
}
