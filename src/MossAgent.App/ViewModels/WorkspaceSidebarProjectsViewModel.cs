using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Settings;
using MossAgent.App.Resources;
using MossAgent.App.Controls;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSidebarProjectsViewModel : ObservableObject, IDisposable
{
    private readonly ISidebarPreferenceStore? _store;
    private readonly WorkspaceSidebarProjectList _list;
    private readonly CancellationTokenSource _lifetime = new();
    private SidebarPreference _preference = new(SidebarProjectSortOrder.UpdatedAt, []);
    private Task? _loadTask;
    private bool _loaded;
    private bool _busy;
    private bool _disposed;
    private string _status = string.Empty;

    public WorkspaceSidebarProjectsViewModel(
        ObservableCollection<WorkspaceProjectNavigationViewModel> source, ISidebarPreferenceStore? store = null)
    {
        _store = store;
        _loaded = store is null;
        _list = new WorkspaceSidebarProjectList(source);
        Items.CollectionChanged += HandleItemsChanged;
        SortByActivityCommand = new GuardedAsyncRelayCommand(token => ChangeSortAsync(SidebarProjectSortOrder.UpdatedAt, token), CanChange);
        SortManuallyCommand = new GuardedAsyncRelayCommand(token => ChangeSortAsync(SidebarProjectSortOrder.Manual, token), CanChange);
        MoveUpCommand = new GuardedAsyncRelayCommand<WorkspaceProjectNavigationViewModel>(
            (project, token) => MoveAsync(project, -1, token), project => CanMove(project, -1));
        MoveDownCommand = new GuardedAsyncRelayCommand<WorkspaceProjectNavigationViewModel>(
            (project, token) => MoveAsync(project, 1, token), project => CanMove(project, 1));
        RetryLoadCommand = new GuardedAsyncRelayCommand(LoadCoreAsync, () => !_disposed && !_busy);
        ReorderCommand = new GuardedAsyncRelayCommand<AppProjectReorderRequest>(ReorderAsync, CanReorder);
    }

    public ObservableCollection<WorkspaceProjectNavigationViewModel> Items => _list.Items;
    public bool IsActivitySort => _preference.ProjectSortOrder == SidebarProjectSortOrder.UpdatedAt;
    public bool IsManualSort => _preference.ProjectSortOrder == SidebarProjectSortOrder.Manual;
    public bool IsBusy => _busy;
    public object ReorderScope { get; } = new();
    public bool IsReorderAvailable => CanChange() && Items.Count > 1;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasError => Status.Length > 0;
    public string SortHeading => SidebarText.Get(nameof(SortHeading));
    public string SortByActivityLabel => SidebarText.Get(nameof(SortByActivityLabel));
    public string SortManuallyLabel => SidebarText.Get(nameof(SortManuallyLabel));
    public string MoveUpLabel => SidebarText.Get(nameof(MoveUpLabel));
    public string MoveDownLabel => SidebarText.Get(nameof(MoveDownLabel));
    public string RetryLabel => SidebarText.Get(nameof(RetryLabel));
    public GuardedAsyncRelayCommand SortByActivityCommand { get; }
    public GuardedAsyncRelayCommand SortManuallyCommand { get; }
    public GuardedAsyncRelayCommand<WorkspaceProjectNavigationViewModel> MoveUpCommand { get; }
    public GuardedAsyncRelayCommand<WorkspaceProjectNavigationViewModel> MoveDownCommand { get; }
    public GuardedAsyncRelayCommand RetryLoadCommand { get; }
    public GuardedAsyncRelayCommand<AppProjectReorderRequest> ReorderCommand { get; }

    public Task LoadAsync() => _disposed ? Task.CompletedTask : _loadTask ??= LoadCoreAsync(CancellationToken.None);

    private async Task LoadCoreAsync(CancellationToken token)
    {
        if (_disposed || _busy) return;
        SetBusy(true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var preference = _store is null ? _preference : await _store.LoadAsync(timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            Apply(preference);
            _loaded = true;
            SetStatus(string.Empty);
        }
        catch (Exception) when (!_disposed)
        {
            _loaded = false;
            SetStatus(SidebarText.Get("LoadFailed"));
        }
        catch (Exception) when (_disposed) { }
        finally { if (!_disposed) SetBusy(false); }
    }

    private Task ChangeSortAsync(SidebarProjectSortOrder order, CancellationToken token)
    {
        if (!CanChange() || _preference.ProjectSortOrder == order) return Task.CompletedTask;
        var manual = order == SidebarProjectSortOrder.Manual && _preference.ManualProjectOrder.Count == 0
            ? Items.Select(item => item.Project.Id).ToArray() : _preference.ManualProjectOrder;
        return SaveAsync(new SidebarPreference(order, manual), token);
    }

    private Task MoveAsync(WorkspaceProjectNavigationViewModel? project, int direction, CancellationToken token)
    {
        if (!CanMove(project, direction)) return Task.CompletedTask;
        var order = Items.Select(item => item.Project.Id).ToList();
        var index = Items.IndexOf(project!);
        (order[index], order[index + direction]) = (order[index + direction], order[index]);
        project!.IsSummaryOpen = false;
        return SaveAsync(new SidebarPreference(SidebarProjectSortOrder.Manual, order), token);
    }

    private async Task SaveAsync(SidebarPreference preference, CancellationToken token)
    {
        SetBusy(true);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            if (_store is not null) await _store.SaveAsync(preference, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            Apply(preference);
            SetStatus(string.Empty);
        }
        catch (Exception) when (!_disposed) { SetStatus(SidebarText.Get("SaveFailed")); }
        catch (Exception) when (_disposed) { }
        finally { if (!_disposed) SetBusy(false); }
    }

    private bool CanReorder(AppProjectReorderRequest? request) => TryGetReorderIndices(request, out _, out _);

    private bool TryGetReorderIndices(AppProjectReorderRequest? request, out int sourceIndex, out int targetIndex)
    {
        sourceIndex = targetIndex = -1;
        if (!CanChange() || request is null || !ReferenceEquals(request.Scope, ReorderScope)) return false;
        for (var index = 0; index < Items.Count; index++)
        {
            var project = Items[index].Project;
            if (sourceIndex < 0 && ReferenceEquals(project, request.Source)) sourceIndex = index;
            if (targetIndex < 0 && ReferenceEquals(project, request.Target)) targetIndex = index;
            if (sourceIndex >= 0 && targetIndex >= 0) return true;
        }
        return false;
    }

    private Task ReorderAsync(AppProjectReorderRequest? request, CancellationToken token)
    {
        if (!TryGetReorderIndices(request, out var sourceIndex, out var targetIndex)) return Task.CompletedTask;
        var insertionIndex = targetIndex + (request!.InsertAfter ? 1 : 0);
        if (sourceIndex < insertionIndex) insertionIndex--;
        if (sourceIndex == targetIndex || sourceIndex == insertionIndex) return Task.CompletedTask;
        var source = Items[sourceIndex];
        var target = Items[targetIndex];
        var order = Items.Select(item => item.Project.Id).ToList();
        order.RemoveAt(sourceIndex);
        order.Insert(insertionIndex, source.Project.Id);
        source.IsSummaryOpen = false;
        target.IsSummaryOpen = false;
        return SaveAsync(new SidebarPreference(SidebarProjectSortOrder.Manual, order), token);
    }

    private void Apply(SidebarPreference preference)
    {
        _preference = preference;
        _list.SetPreference(preference);
        OnPropertyChanged(nameof(IsActivitySort));
        OnPropertyChanged(nameof(IsManualSort));
    }

    private bool CanChange() => !_disposed && !_busy && _loaded;
    private bool CanMove(WorkspaceProjectNavigationViewModel? project, int direction) =>
        CanChange() && project is not null && Items.IndexOf(project) is var index
        && index >= 0 && index + direction >= 0 && index + direction < Items.Count;

    private void SetBusy(bool value)
    {
        SetProperty(ref _busy, value, nameof(IsBusy));
        NotifyCommands();
    }

    private void SetStatus(string value)
    {
        Status = value;
        OnPropertyChanged(nameof(HasError));
    }

    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) => NotifyCommands();
    private void NotifyCommands()
    {
        SortByActivityCommand.NotifyCanExecuteChanged();
        SortManuallyCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        RetryLoadCommand.NotifyCanExecuteChanged();
        ReorderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsReorderAvailable));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Items.CollectionChanged -= HandleItemsChanged;
        _list.Dispose();
        SortByActivityCommand.Dispose();
        SortManuallyCommand.Dispose();
        RetryLoadCommand.Dispose();
        MoveUpCommand.Dispose();
        MoveDownCommand.Dispose();
        ReorderCommand.Dispose();
        NotifyCommands();
    }
}
