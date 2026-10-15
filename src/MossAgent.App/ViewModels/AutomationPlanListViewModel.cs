using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Application.Automations;
using MossAgent.Application.Persistence;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AutomationPlanListViewModel(IAutomationRepository repository, IWorkspaceRepository projects,
    EnabledModelCatalog models, AutomationScheduler scheduler) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _refreshTask;
    private AutomationItemViewModel? _selected;
    private string? _loadError;
    private bool _loading;
    private bool _refreshAgain;
    private bool _disposed;
    private int _snapshotVersion;

    public ObservableCollection<AutomationItemViewModel> Items { get; } = [];
    public bool IsLoading { get => _loading; private set { if (SetProperty(ref _loading, value)) NotifyState(); } }
    public string? LoadError { get => _loadError; private set { if (SetProperty(ref _loadError, value)) NotifyState(); } }
    public int SnapshotVersion { get => _snapshotVersion; private set => SetProperty(ref _snapshotVersion, value); }
    public bool HasLoadError => LoadError is not null;
    public bool HasNotice => IsLoading || HasLoadError;
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => !IsLoading && !HasLoadError && !HasItems;
    public bool ShowLoading => IsLoading && !HasItems;
    public bool ShowFailure => HasLoadError && !HasItems;
    public bool IsRefreshing => IsLoading && HasItems;
    public bool ShowRetainedError => HasLoadError && HasItems;
    public bool HasSelection => SelectedItem is not null;
    public AutomationItemViewModel? SelectedItem
    {
        get => _selected;
        set
        {
            if (_disposed || value is not null && !Owns(value) || !SetProperty(ref _selected, value)) return;
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool Owns(AutomationItemViewModel? item) => !_disposed && item is not null
        && Items.Contains(item);

    public Task ReloadAsync(bool requireFresh = false)
    {
        if (_disposed) return Task.CompletedTask;
        if (_refreshTask is { IsCompleted: false })
        {
            _refreshAgain |= requireFresh;
            return _refreshTask;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _refreshTask = completion.Task;
        _ = CompleteReadAsync(completion);
        return _refreshTask;
    }

    private async Task CompleteReadAsync(TaskCompletionSource completion)
    {
        try { await ReadAsync(); completion.SetResult(); }
        catch (Exception exception) { completion.SetException(exception); }
    }

    private async Task ReadAsync()
    {
        IsLoading = true;
        LoadError = null;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                _refreshAgain = false;
                var schedules = await repository.GetAllAsync(cancellation.Token);
                var availableProjects = await projects.GetProjectsAsync(cancellation.Token);
                var availableModels = await models.ReadAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if (_disposed) return;
                var selection = SelectedItem?.Schedule.Id;
                Items.ReplaceWith(CreateSnapshot(schedules, availableProjects, availableModels));
                SelectedItem = Items.FirstOrDefault(item => item.Schedule.Id == selection) ?? Items.FirstOrDefault();
                SnapshotVersion++;
                NotifyState();
            }
            while (_refreshAgain && !_disposed);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed) LoadError = AutomationText.Get("LoadError"); }
        finally { IsLoading = false; }
    }

    private AutomationItemViewModel[] CreateSnapshot(IReadOnlyList<AutomationSchedule> schedules,
        IReadOnlyList<ProjectProfile> availableProjects, IReadOnlyList<ModelPickerItemViewModel> availableModels)
    {
        if (schedules.Count == 0) return [];
        var projectsById = new Dictionary<Guid, ProjectProfile>(availableProjects.Count);
        foreach (var project in availableProjects) projectsById.TryAdd(project.Id, project);
        var modelsById = new Dictionary<(Guid, Guid), ModelPickerItemViewModel>(availableModels.Count);
        foreach (var model in availableModels) modelsById.TryAdd((model.Provider.Id, model.Model.Id), model);
        var unavailable = AutomationText.Get("Unavailable");
        var snapshot = new AutomationItemViewModel[schedules.Count];
        for (var index = 0; index < schedules.Count; index++)
        {
            var schedule = schedules[index];
            snapshot[index] = new AutomationItemViewModel(schedule,
                projectsById.GetValueOrDefault(schedule.ProjectId)?.Name ?? unavailable,
                modelsById.GetValueOrDefault((schedule.ProviderId, schedule.ModelId))?.AccessibleName ?? unavailable,
                scheduler.IsRunning(schedule.Id));
        }
        return snapshot;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasLoadError)); OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowLoading)); OnPropertyChanged(nameof(ShowFailure)); OnPropertyChanged(nameof(IsRefreshing));
        OnPropertyChanged(nameof(ShowRetainedError));
        OnPropertyChanged(nameof(HasNotice));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        Items.Clear();
        _selected = null;
        NotifyState();
        OnPropertyChanged(nameof(SelectedItem)); OnPropertyChanged(nameof(HasSelection));
    }
}
