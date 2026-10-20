using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSteeringHistoryViewModel : ObservableObject, IDisposable
{
    private readonly IConversationSteeringRepository? _repository;
    private readonly WorkspaceCatalogViewModel _catalog;
    private readonly WorkspaceFollowupOperations _operations = new();
    private long _before = long.MaxValue;
    private Guid? _selectedTaskId;
    private int _generation;
    private bool _loading;
    private bool _hasEarlier;
    private bool _failed;
    private bool _disposed;

    internal WorkspaceSteeringHistoryViewModel(IConversationSteeringRepository? repository, WorkspaceCatalogViewModel catalog)
    {
        _repository = repository; _catalog = catalog;
        _selectedTaskId = catalog.SelectedTask?.Id;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanLoad, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        EarlierCommand = new AsyncRelayCommand(() => LoadAsync(_before), () => CanLoad() && _hasEarlier);
        _catalog.SelectionChanged += SelectTask;
    }

    public ObservableCollection<WorkspaceSteeringReceiptViewModel> Items { get; } = [];
    public bool IsVisible => Items.Count > 0 || _loading || _failed;
    public bool IsLoading => _loading;
    public bool HasNotice => _loading || _failed;
    public string Notice => WorkspaceText.Get(_loading ? "SteeringHistoryLoading" : "SteeringHistoryFailed");
    public string Title => WorkspaceText.Get("SteeringHistoryTitle");
    public string RefreshLabel => WorkspaceText.Get("SteeringHistoryRefresh");
    public string EarlierLabel => WorkspaceText.Get("SteeringHistoryEarlier");
    public bool HasEarlier => _hasEarlier;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand EarlierCommand { get; }

    private bool CanLoad() => !_disposed && !_operations.IsStopping && !_loading && _repository is not null && _catalog.SelectedTask is not null;

    private void SelectTask()
    {
        if (_selectedTaskId == _catalog.SelectedTask?.Id) { _ = RefreshAsync(); return; }
        _selectedTaskId = _catalog.SelectedTask?.Id;
        _generation++; _before = long.MaxValue; _loading = _hasEarlier = _failed = false;
        Items.Clear(); Notify(); _ = RefreshAsync();
    }

    internal Task RefreshForTaskAsync(Guid taskId) => !_disposed && _catalog.SelectedTask?.Id == taskId ? RefreshAsync() : Task.CompletedTask;
    internal Task RefreshAsync() => LoadAsync(long.MaxValue);

    private async Task LoadAsync(long before)
    {
        if (_disposed || _operations.IsStopping || _repository is null || _catalog.SelectedTask is not { } task) return;
        var generation = _generation;
        _loading = true; Notify();
        try
        {
            await _operations.RunAsync(task.Id, false, token => LoadPageAsync(task.Id, before, generation, token));
        }
        catch (OperationCanceledException) when (_disposed || _operations.IsStopping) { }
        catch (ObjectDisposedException) when (_disposed || _operations.IsStopping) { }
        catch (Exception) { await ReportFailureAsync(generation); }
        finally
        {
            await AvaloniaUiThread.RunAsync(() =>
            { if (!_disposed && generation == _generation) { _loading = false; Notify(); } }, CancellationToken.None);
        }
    }

    private async Task LoadPageAsync(Guid taskId, long before, int generation, CancellationToken token)
    {
        try
        {
            var page = await _repository!.GetInputsAsync(taskId, before, ConversationSteeringInput.MaximumPageSize, token);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || generation != _generation || _catalog.SelectedTask?.Id != taskId) return;
                var previous = Items.ToDictionary(item => item.Id);
                var rows = page.Items.Select(input =>
                {
                    if (!previous.TryGetValue(input.Id, out var row)) row = new(input);
                    else row.Update(input);
                    return row;
                }).ToArray();
                Items.ReplaceWith(rows);
                _hasEarlier = page.HasMore; _before = page.Items.LastOrDefault()?.Sequence ?? long.MaxValue;
                _failed = false;
            }, token);
        }
        catch (Exception) { await ReportFailureAsync(generation); }
    }

    private Task ReportFailureAsync(int generation) => AvaloniaUiThread.RunAsync(() =>
    { if (!_disposed && !_operations.IsStopping && generation == _generation) _failed = true; }, CancellationToken.None);

    private void Notify()
    {
        foreach (var name in new[] { nameof(IsVisible), nameof(IsLoading), nameof(HasNotice), nameof(Notice), nameof(HasEarlier) }) OnPropertyChanged(name);
        RefreshCommand.NotifyCanExecuteChanged(); EarlierCommand.NotifyCanExecuteChanged();
    }

    public Task StopAsync() => _operations.StopAsync();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _generation++; _catalog.SelectionChanged -= SelectTask; _operations.Dispose();
    }
}
