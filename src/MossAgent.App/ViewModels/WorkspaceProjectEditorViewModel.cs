using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Authorization;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceProjectEditorViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceProjectService _projectService;
    private string _name = string.Empty;
    private string _status = string.Empty;
    private bool _isOpen;
    private ProjectProfile? _editingProject;
    private readonly CancellationTokenSource _lifetime = new();
    private long _draftRevision;
    private long _editorVersion;
    private bool _isSaving;
    private bool _isPickingDirectory;
    private bool _hasStatusError;
    private bool _disposed;

    public WorkspaceProjectEditorViewModel(WorkspaceProjectService projectService)
    {
        _projectService = projectService;
        AuthorizationResources = new(projectService);
        WorktreeRecovery = new(projectService);
        OpenCommand = new RelayCommand(OpenNew, CanDismiss);
        CloseCommand = new RelayCommand(Close, CanDismiss);
        AddCommand = new GuardedAsyncRelayCommand(AddAsync, CanMutate);
        AddDirectoryCommand = new GuardedAsyncRelayCommand(AddDirectoryAsync, CanMutate);
        ConfirmRootObjectsCommand = new GuardedAsyncRelayCommand(ConfirmRootObjectsAsync, CanConfirmRootObjects);
        Directories.CollectionChanged += HandleDirectoriesChanged;
    }

    public event Action<ProjectProfile>? ProjectCreated;

    public event Action<ProjectProfile>? ProjectUpdated;

    public Func<bool> CanSave { get; set; } = static () => true;

    public IAsyncRelayCommand AddCommand { get; }

    public IRelayCommand OpenCommand { get; }

    public IAsyncRelayCommand AddDirectoryCommand { get; }
    public IAsyncRelayCommand ConfirmRootObjectsCommand { get; }
    public WorkspaceAuthorizationResourcesViewModel AuthorizationResources { get; }
    public WorkspaceWorktreeRecoveryViewModel WorktreeRecovery { get; }
    public bool ShowRootConfirmation => _editingProject is not null && _projectService.CanConfirmRootObjects;
    public string RootConfirmationLabel => SettingsText.Get("ProjectConfirmRootObjects");
    public string RootConfirmationHint => SettingsText.Get("ProjectConfirmRootObjectsHint");

    public IRelayCommand CloseCommand { get; }

    public ObservableCollection<WorkspaceProjectDirectoryViewModel> Directories { get; } = [];
    public bool HasDirectories => Directories.Count > 0;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    public string EditorTitle => SettingsText.Get(_editingProject is null ? "ProjectCreateTitle" : "ProjectEditTitle");

    public string SubmitLabel => SettingsText.Get(IsSaving ? "ProjectSaving"
        : _editingProject is null ? "ProjectCreateAction" : "ProjectSaveAction");

    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) _draftRevision++; }
    }

    public string Status
    {
        get => _status;
        private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); }
    }

    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public bool IsSaving { get => _isSaving; private set { if (SetProperty(ref _isSaving, value)) NotifyBusyChanged(); } }
    public bool IsPickingDirectory
    {
        get => _isPickingDirectory;
        private set { if (SetProperty(ref _isPickingDirectory, value)) NotifyBusyChanged(); }
    }
    public bool IsBusy => IsSaving || IsPickingDirectory;

    public void Edit(ProjectProfile? project)
    {
        if (project is null || _disposed) return;
        _editorVersion++;
        _editingProject = project;
        Name = project.Name;
        Directories.Clear();
        foreach (var path in project.AuthorizedDirectories)
        {
            Directories.Add(new WorkspaceProjectDirectoryViewModel(
                path, Directories.Count == 0, SetPrimary, Remove));
        }
        Status = string.Empty;
        HasStatusError = false;
        if (ShowRootConfirmation && project.RootAuthorizations.Count == 0) SetStatus("ProjectRootsNeedConfirmation", false);
        AuthorizationResources.Show(project.Id);
        WorktreeRecovery.Show(project.Id);
        NotifyEditorModeChanged();
        IsOpen = true;
    }

    private void OpenNew()
    {
        if (!CanDismiss()) return;
        _editorVersion++;
        if (_editingProject is not null)
        {
            _editingProject = null;
            Name = string.Empty;
            Directories.Clear();
            Status = string.Empty;
            HasStatusError = false;
            AuthorizationResources.Clear();
            WorktreeRecovery.Clear();
            NotifyEditorModeChanged();
        }
        IsOpen = true;
    }

    private void Close()
    {
        if (!CanDismiss()) return;
        IsOpen = false;
        AuthorizationResources.Clear();
        WorktreeRecovery.Clear();
    }

    private async Task AddAsync(CancellationToken cancellationToken)
    {
        if (_disposed || IsBusy) return;
        if (!CanSave())
        {
            SetStatus("ProjectMutationLocked", true);
            return;
        }
        var version = _editorVersion;
        var revision = _draftRevision;
        var name = Name;
        var paths = Directories.Select(static directory => directory.Path).ToArray();
        var editingProject = _editingProject;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(30));
        IsSaving = true;
        Status = string.Empty;
        HasStatusError = false;
        try
        {
            var project = editingProject is null
                ? await _projectService.CreateAsync(name, paths, operation.Token)
                : await _projectService.UpdateAsync(editingProject, name, paths, operation.Token);
            if (!_disposed)
                PublishSavedProject(project, editingProject is null, version, revision);
        }
        catch (ProjectAuthorizationNotQuiescedException exception)
        {
            if (IsCurrent(version))
                PublishSavedProject(exception.SavedProject, editingProject is null, version, revision, drainPending: true);
        }
        catch (WorkspaceProjectValidationException exception)
        {
            if (IsCurrent(version) && revision == _draftRevision)
                SetStatus(exception.Failure == WorkspaceProjectValidationFailure.RequiredFields
                    ? "ProjectRequiredFields" : "ProjectInvalidDirectories", true);
        }
        catch (ProjectRootAuthorizationException)
        {
            if (IsCurrent(version)) SetStatus("ProjectRootIdentityFailed", true);
        }
        catch (ProjectAuthorizationConflictException)
        {
            if (IsCurrent(version)) SetStatus("ProjectAuthorizationConflict", true);
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (IsCurrent(version)) SetStatus(cancellationToken.IsCancellationRequested
                ? "ProjectSaveCancelled" : "ProjectSaveTimedOut", true);
        }
        catch (Exception) { if (IsCurrent(version)) SetStatus("ProjectSaveFailed", true); }
        finally { IsSaving = false; }
    }

    private async Task ConfirmRootObjectsAsync(CancellationToken cancellationToken)
    {
        if (!CanConfirmRootObjects()) return;
        var expected = _editingProject!;
        var version = _editorVersion;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(30));
        IsSaving = true;
        try
        {
            var saved = await _projectService.ConfirmRootObjectsAsync(expected, operation.Token);
            if (!IsCurrent(version)) return;
            _editingProject = saved;
            ProjectUpdated?.Invoke(saved);
            AuthorizationResources.Show(saved.Id);
            SetStatus("ProjectRootObjectsConfirmed", false);
        }
        catch (ProjectAuthorizationNotQuiescedException exception)
        {
            if (!IsCurrent(version)) return;
            _editingProject = exception.SavedProject;
            ProjectUpdated?.Invoke(exception.SavedProject);
            AuthorizationResources.Show(exception.SavedProject.Id);
            SetStatus("ProjectAuthorizationSavedDrainPending", true);
            NotifyEditorModeChanged();
        }
        catch (ProjectAuthorizationConflictException) { if (IsCurrent(version)) SetStatus("ProjectAuthorizationConflict", true); }
        catch (OperationCanceledException) { if (IsCurrent(version)) SetStatus("ProjectSaveCancelled", true); }
        catch (Exception) { if (IsCurrent(version)) SetStatus("ProjectRootIdentityFailed", true); }
        finally { IsSaving = false; }
    }

    private bool CanConfirmRootObjects() => CanMutate() && ShowRootConfirmation
        && _editingProject!.AuthorizedDirectories.SequenceEqual(Directories.Select(item => item.Path),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private async Task AddDirectoryAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate()) return;
        var version = _editorVersion;
        var revision = _draftRevision;
        var name = Name;
        var restoreEditor = IsOpen;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        IsPickingDirectory = true;
        try
        {
            var selection = await _projectService.PickAsync(name, operation.Token);
            if (!IsCurrent(version) || operation.IsCancellationRequested) return;
            if (restoreEditor) { IsOpen = false; IsOpen = true; }
            if (selection is null) return;
            if (Directories.Any(directory => string.Equals(
                    directory.Path, selection.Value.Path, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus("ProjectDuplicateDirectory", true);
                return;
            }
            var useSuggestedName = revision == _draftRevision && string.IsNullOrWhiteSpace(Name);
            Directories.Add(new WorkspaceProjectDirectoryViewModel(
                selection.Value.Path, Directories.Count == 0, SetPrimary, Remove));
            if (useSuggestedName) Name = selection.Value.SuggestedName;
            Status = string.Empty;
            HasStatusError = false;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
        catch (Exception) { if (IsCurrent(version)) SetStatus("ProjectDirectoryPickFailed", true); }
        finally
        {
            IsPickingDirectory = false;
            if (restoreEditor && IsCurrent(version)) IsOpen = true;
        }
    }

    private void PublishSavedProject(ProjectProfile project, bool created, long version, long revision,
        bool drainPending = false)
    {
        if (created) ProjectCreated?.Invoke(project);
        else ProjectUpdated?.Invoke(project);
        if (!IsCurrent(version)) return;
        if (drainPending)
        {
            if (_editingProject?.Id == project.Id) _editingProject = project;
            AuthorizationResources.Show(project.Id);
            WorktreeRecovery.Show(project.Id);
            SetStatus("ProjectAuthorizationSavedDrainPending", true);
            NotifyEditorModeChanged();
            return;
        }
        if (revision != _draftRevision)
        {
            if (_editingProject?.Id == project.Id) _editingProject = project;
            SetStatus("ProjectPreviousDraftSaved", false);
            return;
        }
        Name = string.Empty;
        Directories.Clear();
        AuthorizationResources.Clear();
        WorktreeRecovery.Clear();
        SetStatus(created ? "ProjectCreated" : "ProjectSaved", false);
        _editingProject = null;
        NotifyEditorModeChanged();
        IsOpen = false;
    }

    private void SetPrimary(WorkspaceProjectDirectoryViewModel selected)
    {
        if (_disposed) return;
        var index = Directories.IndexOf(selected);
        if (index <= 0) return;
        Directories.Move(index, 0);
        UpdatePrimaryStates();
    }

    private void Remove(WorkspaceProjectDirectoryViewModel selected)
    {
        if (_disposed) return;
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

    private void NotifyEditorModeChanged()
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(SubmitLabel));
        OnPropertyChanged(nameof(ShowRootConfirmation));
    }

    public void NotifyAvailabilityChanged()
    {
        if (!_disposed && IsOpen && !CanSave()) SetStatus("ProjectMutationLocked", true);
        AddCommand.NotifyCanExecuteChanged();
        AddDirectoryCommand.NotifyCanExecuteChanged();
        ConfirmRootObjectsCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    private void NotifyBusyChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(SubmitLabel));
        NotifyAvailabilityChanged();
    }

    private void SetStatus(string key, bool error)
    {
        Status = SettingsText.Get(key);
        HasStatusError = error;
    }

    private bool CanMutate() => !_disposed && !IsBusy && CanSave();
    private bool CanDismiss() => !_disposed && !IsBusy;
    private bool IsCurrent(long version) => !_disposed && version == _editorVersion;
    private void HandleDirectoriesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        _draftRevision++;
        OnPropertyChanged(nameof(HasDirectories));
        ConfirmRootObjectsCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Directories.CollectionChanged -= HandleDirectoriesChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        ((IDisposable)AddCommand).Dispose();
        ((IDisposable)AddDirectoryCommand).Dispose();
        ((IDisposable)ConfirmRootObjectsCommand).Dispose();
        AuthorizationResources.Dispose();
        WorktreeRecovery.Dispose();
        NotifyAvailabilityChanged();
    }
}
