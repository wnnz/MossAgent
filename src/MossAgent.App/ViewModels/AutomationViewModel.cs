using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Automations;
using MossAgent.Application.Persistence;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AutomationViewModel : ObservableObject, IDisposable
{
    private readonly IAutomationRepository _repository;
    private readonly AutomationScheduler _scheduler;
    private readonly AutomationEditorActions _editorActions;
    private readonly CancellationTokenSource _lifetime = new();
    private AutomationItemViewModel? _pendingDelete;
    private string? _error;
    private string? _notice;
    private bool _disposed;

    public AutomationViewModel(IAutomationRepository repository, AutomationScheduler scheduler,
        IConfigurationRepository configurations, IWorkspaceRepository projects, WorkspaceCatalogViewModel catalog,
        TimeProvider? clock = null)
    {
        _repository = repository;
        _scheduler = scheduler;
        var models = new EnabledModelCatalog(configurations);
        clock ??= TimeProvider.System;
        Operation = new AutomationOperationViewModel(clock);
        Editor = new AutomationEditorViewModel(clock);
        List = new AutomationPlanListViewModel(repository, projects, models, scheduler);
        _editorActions = new AutomationEditorActions(repository, projects, models, List, Editor, Operation, scheduler,
            clock, error => { if (!_disposed) Error = error; }, notice => { if (!_disposed) Notice = notice; });
        History = new AutomationHistoryViewModel(repository, projects, catalog);
        History.TaskOpened += HandleTaskOpened;
        RefreshCommand = new AsyncRelayCommand(ReloadAsync, () => !IsBusy && !List.IsLoading && !_disposed);
        CreateCommand = new GuardedAsyncRelayCommand(token => EditAsync(null, token), CanCreate);
        EditCommand = new GuardedAsyncRelayCommand<AutomationItemViewModel>(EditAsync, CanActOnRow);
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, () => CanUseSnapshot && Editor.IsOpen);
        CloseEditorCommand = new RelayCommand(CloseEditor, () => !IsBusy && !_disposed);
        ToggleCommand = new GuardedAsyncRelayCommand<AutomationItemViewModel>(ToggleAsync, CanActOnRow);
        RunNowCommand = new GuardedAsyncRelayCommand<AutomationItemViewModel>(RunNowAsync, CanRunNow);
        StopCommand = new RelayCommand<AutomationItemViewModel>(StopRun, CanStop);
        RemoveCommand = new RelayCommand<AutomationItemViewModel>(RequestDelete,
            item => CanActOnRow(item) && !_scheduler.IsRunning(item!.Schedule.Id));
        CancelDeleteCommand = new RelayCommand(CancelDelete, () => !IsBusy && !_disposed);
        ConfirmDeleteCommand = new GuardedAsyncRelayCommand(DeleteAsync, CanDelete);
        List.PropertyChanged += HandleListChanged;
        Editor.PropertyChanged += HandleEditorChanged;
        Operation.PropertyChanged += HandleOperationChanged;
        _scheduler.StateChanged += HandleSchedulerChanged;
    }

    public event Action? TaskOpened;
    public AutomationPlanListViewModel List { get; }
    public ObservableCollection<AutomationItemViewModel> Items => List.Items;
    public AutomationHistoryViewModel History { get; }
    public ObservableCollection<AutomationRunViewModel> Runs => History.Runs;
    public AutomationEditorViewModel Editor { get; }
    public AutomationOperationViewModel Operation { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand CreateCommand { get; }
    public IAsyncRelayCommand<AutomationItemViewModel> EditCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand CloseEditorCommand { get; }
    public IAsyncRelayCommand<AutomationItemViewModel> ToggleCommand { get; }
    public IAsyncRelayCommand<AutomationItemViewModel> RunNowCommand { get; }
    public IRelayCommand<AutomationItemViewModel> StopCommand { get; }
    public IRelayCommand<AutomationItemViewModel> RemoveCommand { get; }
    public IRelayCommand CancelDeleteCommand { get; }
    public IAsyncRelayCommand ConfirmDeleteCommand { get; }
    public IAsyncRelayCommand<AutomationRunViewModel> OpenRunCommand => History.OpenCommand;
    public bool IsBusy => Operation.IsRunning;
    public string? Notice { get => _notice; private set { if (SetProperty(ref _notice, value)) { OnPropertyChanged(nameof(HasNotice)); OnPropertyChanged(nameof(HasOperationNotice)); } } }
    public bool HasNotice => !string.IsNullOrEmpty(Notice);
    public bool HasOperationNotice => IsBusy || HasNotice;
    public bool IsEmpty => List.IsEmpty;
    public bool HasSelection => SelectedItem is not null;
    public bool IsDeleteOpen => _pendingDelete is not null;
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string RuntimeHint => AutomationText.Get(_scheduler.ErrorCode == "another-instance" ? "AnotherInstance"
        : _scheduler.ErrorCode is not null ? "RuntimeError" : !_scheduler.IsAvailable ? "NotReady" : "Subtitle");
    public AutomationItemViewModel? SelectedItem
    {
        get => List.SelectedItem;
        set => List.SelectedItem = value;
    }

    public Task ReloadAsync() => List.ReloadAsync();
    private bool CanUseSnapshot => !IsBusy && !_disposed && !List.IsLoading && !List.HasLoadError;
    private bool CanCreate() => CanUseSnapshot && !Editor.IsOpen && !IsDeleteOpen;
    private bool CanActOnRow(AutomationItemViewModel? item) => CanCreate() && List.Owns(item);
    private bool CanStop(AutomationItemViewModel? item) => List.Owns(item) && !_disposed && _scheduler.IsRunning(item!.Schedule.Id);
    private void StopRun(AutomationItemViewModel? item) { if (CanStop(item)) _scheduler.StopRun(item!.Schedule.Id); }
    private bool CanDelete() => CanUseSnapshot && List.Owns(_pendingDelete) && !_scheduler.IsRunning(_pendingDelete!.Schedule.Id);

    private Task EditAsync(AutomationItemViewModel? item, CancellationToken token)
    {
        return (item is null ? !CanCreate() : !CanActOnRow(item)) ? Task.CompletedTask : _editorActions.EditAsync(item?.Schedule, token);
    }

    private Task SaveAsync(CancellationToken token) => !CanUseSnapshot || !Editor.IsOpen ? Task.CompletedTask : _editorActions.SaveAsync(token);
    private void CloseEditor() { if (IsBusy || _disposed) return; Editor.IsOpen = false; Notice = null; }

    private async Task ToggleAsync(AutomationItemViewModel? item, CancellationToken token)
    {
        if (!CanActOnRow(item)) return;
        var schedule = item!.Schedule with { IsEnabled = !item.Schedule.IsEnabled };
        await MutateAsync(cancellation => _repository.SaveAsync(schedule with
        { NextRunAt = AutomationTiming.Next(schedule, DateTimeOffset.UtcNow) }, cancellation), token, "ChangingPlan");
    }

    private bool CanRunNow(AutomationItemViewModel? item) => CanActOnRow(item)
        && _scheduler.IsAvailable && !_scheduler.IsRunning(item!.Schedule.Id);
    private Task RunNowAsync(AutomationItemViewModel? item, CancellationToken token) => !CanRunNow(item) ? Task.CompletedTask
        : MutateAsync(cancellation => _scheduler.RunNowAsync(item!.Schedule.Id, cancellation), token, "StartingRun");
    private async Task DeleteAsync(CancellationToken token)
    {
        if (!CanDelete() || _pendingDelete is not { } item) return;
        await MutateAsync(async cancellation =>
        {
            var archived = await _repository.ArchiveAsync(item.Schedule.Id, item.Schedule.Revision, cancellation);
            if (archived && !_disposed) CancelDelete();
            return archived;
        }, token, "RemovingPlan");
    }
    private void RequestDelete(AutomationItemViewModel? item)
    {
        if (!CanActOnRow(item) || _scheduler.IsRunning(item!.Schedule.Id)) return;
        _pendingDelete = item;
        OnPropertyChanged(nameof(IsDeleteOpen));
        NotifyCommands();
    }
    private void CancelDelete() { _pendingDelete = null; OnPropertyChanged(nameof(IsDeleteOpen)); NotifyCommands(); }

    private void HandleEditorChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(AutomationEditorViewModel.IsOpen)) NotifyCommands(); }

    private void HandleOperationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(AutomationOperationViewModel.IsRunning)) return;
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(HasOperationNotice));
        NotifyCommands();
    }

    private void HandleListChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed) return;
        if (args.PropertyName == nameof(AutomationPlanListViewModel.SelectedItem))
        {
            OnPropertyChanged(nameof(SelectedItem)); OnPropertyChanged(nameof(HasSelection));
            _ = History.SelectAsync(SelectedItem);
        }
        if (args.PropertyName == nameof(AutomationPlanListViewModel.SnapshotVersion)) _editorActions.HandleSnapshotChanged();
        if (args.PropertyName == nameof(AutomationPlanListViewModel.SnapshotVersion) && _pendingDelete is { } pending)
        {
            _pendingDelete = Items.FirstOrDefault(item => item.Schedule == pending.Schedule);
            if (_pendingDelete is null) { CancelDelete(); Error = AutomationText.Get("Conflict"); }
        }
        OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(RuntimeHint));
        NotifyCommands();
    }

    private Task MutateAsync(Func<CancellationToken, Task<bool>> mutation, CancellationToken token, string progressKey)
    {
        return Operation.RunAsync(async cancellation =>
        {
            Error = null; Notice = null;
            if (!await mutation(cancellation) && !_disposed) Error = AutomationText.Get("Conflict");
        }, token, progressKey, error => { if (!_disposed) Error = error; }, () => List.ReloadAsync(requireFresh: true));
    }

    private void HandleTaskOpened() => TaskOpened?.Invoke();

    private void NotifyCommands()
    {
        History.IsBlocked = IsBusy || List.IsLoading || List.HasLoadError || Editor.IsOpen || IsDeleteOpen;
        RefreshCommand.NotifyCanExecuteChanged(); CreateCommand.NotifyCanExecuteChanged(); EditCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged(); CloseEditorCommand.NotifyCanExecuteChanged(); ToggleCommand.NotifyCanExecuteChanged();
        RunNowCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged(); RemoveCommand.NotifyCanExecuteChanged();
        CancelDeleteCommand.NotifyCanExecuteChanged(); ConfirmDeleteCommand.NotifyCanExecuteChanged(); OpenRunCommand.NotifyCanExecuteChanged();
    }

    private void HandleSchedulerChanged() => _ = RefreshFromSchedulerAsync();
    private async Task RefreshFromSchedulerAsync()
    {
        try { await AvaloniaUiThread.RunAsync(() => List.ReloadAsync(requireFresh: true), _lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scheduler.StateChanged -= HandleSchedulerChanged;
        List.PropertyChanged -= HandleListChanged;
        Editor.PropertyChanged -= HandleEditorChanged;
        Operation.PropertyChanged -= HandleOperationChanged;
        History.TaskOpened -= HandleTaskOpened;
        History.Dispose();
        List.Dispose();
        Operation.Dispose();
        foreach (var command in new IDisposable[] { (IDisposable)CreateCommand, (IDisposable)EditCommand,
            (IDisposable)SaveCommand, (IDisposable)ToggleCommand, (IDisposable)RunNowCommand, (IDisposable)ConfirmDeleteCommand }) command.Dispose();
        _lifetime.Cancel();
        NotifyCommands();
    }
}
