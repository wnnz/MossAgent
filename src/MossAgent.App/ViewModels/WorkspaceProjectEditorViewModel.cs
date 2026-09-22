using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectEditorViewModel : ObservableObject
{
    private readonly WorkspaceProjectService _projectService;
    private string _name = string.Empty;
    private string _directory = string.Empty;
    private string _authorizedDirectories = string.Empty;
    private string _status = string.Empty;
    private bool _isOpen;

    public WorkspaceProjectEditorViewModel(WorkspaceProjectService projectService)
    {
        _projectService = projectService;
        AddCommand = new AsyncRelayCommand(AddAsync);
        BrowseDirectoryCommand = new AsyncRelayCommand(BrowseDirectoryAsync);
    }

    public event Action<ProjectProfile>? ProjectCreated;

    public IAsyncRelayCommand AddCommand { get; }

    public IAsyncRelayCommand BrowseDirectoryCommand { get; }

    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public string Directory { get => _directory; set => SetProperty(ref _directory, value); }

    public string AuthorizedDirectories
    {
        get => _authorizedDirectories;
        set => SetProperty(ref _authorizedDirectories, value);
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }

    private async Task AddAsync()
    {
        try
        {
            var project = await _projectService.CreateAsync(Name, Directory, AuthorizedDirectories);
            ProjectCreated?.Invoke(project);
            Name = string.Empty;
            Status = "项目已添加。";
            IsOpen = false;
        }
        catch (Exception exception)
        {
            Status = exception.Message;
        }
    }

    private async Task BrowseDirectoryAsync()
    {
        var selection = await _projectService.PickAsync(Name);
        if (selection is null)
        {
            return;
        }

        Directory = selection.Value.Path;
        Name = selection.Value.SuggestedName;
    }
}
