using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;
using MossAgent.Application.Git;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskEnvironmentViewModel : ObservableObject
{
    private Guid? _projectId;
    private ProjectProfile? _project;
    private Guid? _taskId;
    private bool _taskIsWorktree;
    private bool _useWorktree;
    private bool _locked;
    private bool _isPickerOpen;
    private string? _preparationNotice;
    private string _baseReference = string.Empty;

    public WorkspaceTaskEnvironmentViewModel(IGitWorktreeReferenceDiscovery? references = null)
    {
        UseProjectDirectoryCommand = new RelayCommand(() => Select(false), () => CanChoose);
        UseWorktreeCommand = new RelayCommand(() => Select(true), () => CanChoose);
        References = new WorkspaceWorktreeReferencePickerViewModel(references, () => _project, () => CanChoose, SelectReference);
    }

    public WorkspaceWorktreeReferencePickerViewModel References { get; }

    public bool HasProject => _projectId is not null;
    public bool CanChoose => HasProject && _taskId is null && !_locked;
    public bool UseWorktree => HasProject && _taskId is null && _useWorktree;
    public bool IsWorktree => _taskId is null ? _useWorktree : _taskIsWorktree;
    public string Label => WorkspaceText.Get(IsWorktree ? "TaskEnvironmentWorktreeLabel" : "TaskEnvironmentProjectLabel");
    public string ProjectDirectoryLabel => WorkspaceText.Get("TaskEnvironmentProjectLabel");
    public string WorktreeLabel => WorkspaceText.Get("TaskEnvironmentWorktreeLabel");
    public string Hint => WorkspaceText.Get(_taskId is null ? "TaskEnvironmentHint" : "TaskEnvironmentExistingHint");
    public string BaseReferenceLabel => WorkspaceText.Get("TaskEnvironmentBaseReferenceLabel");
    public string BaseReferencePlaceholder => WorkspaceText.Get("TaskEnvironmentBaseReferencePlaceholder");
    public string BaseReference
    {
        get => _baseReference;
        set { if (CanChoose) SetProperty(ref _baseReference, value ?? string.Empty); }
    }
    public IRelayCommand UseProjectDirectoryCommand { get; }
    public IRelayCommand UseWorktreeCommand { get; }
    public string? PreparationNotice => _preparationNotice;
    public bool HasPreparationNotice => _preparationNotice is not null;
    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        set
        {
            if (!SetProperty(ref _isPickerOpen, value && CanChoose) || !value) return;
            References.NotifyOpened();
        }
    }

    internal void SetContext(ProjectProfile? project, AgentTask? task)
    {
        var worktree = !string.IsNullOrWhiteSpace(task?.WorktreePath);
        if (_projectId == project?.Id && _taskId == task?.Id && _taskIsWorktree == worktree) return;
        _projectId = project?.Id;
        _project = project;
        _taskId = task?.Id;
        _taskIsWorktree = worktree;
        _useWorktree = false;
        _baseReference = string.Empty;
        OnPropertyChanged(nameof(BaseReference));
        SetPreparationNotice(null);
        References.ResetContext();
        IsPickerOpen = false;
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(Hint));
        NotifyChoice();
    }

    internal void SetLocked(bool locked)
    {
        if (_locked == locked) return;
        _locked = locked;
        if (locked) IsPickerOpen = false;
        NotifyChoice();
    }

    private void Select(bool worktree)
    {
        if (!CanChoose) return;
        _useWorktree = worktree;
        SetPreparationNotice(null);
        IsPickerOpen = false;
        NotifyChoice();
    }

    private void SelectReference(GitWorktreeReference reference)
    {
        if (!CanChoose) return;
        BaseReference = reference.RefName;
    }

    internal void ClearPreparationNotice() => SetPreparationNotice(null);

    internal void ReportPreparationFailure(Exception exception) => SetPreparationNotice(exception switch
    {
        WorkspaceWorktreePreparationException recovery => string.Format(WorkspaceText.Get("TaskEnvironmentRecoveryNotice"), recovery.WorktreePath),
        WorkspacePreparationFinalizationException finalization => string.Format(WorkspaceText.Get("PreparationFinalizationFailure"), finalization.TaskId),
        MossAgent.Application.Models.ModelCapabilityException => WorkspaceText.Get("SendModelCapabilityUnsupported"),
        WorktreeAuthorizationException => WorkspaceText.Get("WorktreeObjectRunFailure"),
        UnauthorizedAccessException => WorkspaceText.Get("TaskEnvironmentAuthorizationFailure"),
        GitWorktreeReferenceException => WorkspaceText.Get("TaskEnvironmentBaseReferenceFailure"),
        _ => null
    });

    private void SetPreparationNotice(string? notice)
    {
        if (SetProperty(ref _preparationNotice, notice, nameof(PreparationNotice)))
            OnPropertyChanged(nameof(HasPreparationNotice));
    }

    private void NotifyChoice()
    {
        OnPropertyChanged(nameof(CanChoose));
        OnPropertyChanged(nameof(UseWorktree));
        OnPropertyChanged(nameof(IsWorktree));
        OnPropertyChanged(nameof(Label));
        UseProjectDirectoryCommand.NotifyCanExecuteChanged();
        UseWorktreeCommand.NotifyCanExecuteChanged();
    }
}
