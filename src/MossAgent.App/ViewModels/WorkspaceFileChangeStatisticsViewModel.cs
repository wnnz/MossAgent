using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Services;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFileChangeStatisticsViewModel(WorkspaceFileChangeStatisticsService? service) : ObservableObject, IDisposable
{
    private readonly object _gate = new();
    private WorkspaceFileChangeViewModel[] _files = [];
    private ToolExecutionContext? _context;
    private long _authorizationRevision;
    private CancellationTokenSource? _cancellation;
    private Task _work = Task.CompletedTask;
    private bool _incomplete;
    private bool _disposed;
    private bool _stopped;
    private bool _isBusy;
    private long _added;
    private long _removed;
    private IRelayCommand? _retry;

    internal bool IsEnabled => service is not null;

    public bool HasTotals { get; private set; }
    public bool HasError { get; private set; }
    public string AddedLabel => $"+{_added.ToString(CultureInfo.CurrentCulture)}";
    public string RemovedLabel => $"-{_removed.ToString(CultureInfo.CurrentCulture)}";
    public string UnavailableHint => WorkspaceText.Get("FileStatisticsUnavailableHint");
    public string RetryLabel => WorkspaceText.Get("FileStatisticsRetry");
    public IRelayCommand RetryCommand => _retry ??= new RelayCommand(
        () => { if (!_disposed && !_stopped && !_isBusy && HasError) Schedule(true); },
        () => !_disposed && !_stopped && !_isBusy && HasError);

    internal void Update(IReadOnlyList<WorkspaceFileChangeViewModel> files, ToolExecutionContext? context, bool incomplete,
        long authorizationRevision = 0)
    {
        if (_disposed || _stopped || service is null) return;
        var sameContext = _authorizationRevision == authorizationRevision
            && _context?.TaskId == context?.TaskId && _context?.WorkingDirectory == context?.WorkingDirectory
            && _context?.ArtifactDirectory == context?.ArtifactDirectory
            && _context?.ApprovalPolicy == context?.ApprovalPolicy
            && (_context?.AuthorizedRoots ?? []).SequenceEqual(context?.AuthorizedRoots ?? []);
        var sameFiles = _files.SequenceEqual(files);
        if (sameContext && sameFiles && _incomplete == incomplete) return;
        if (!sameContext)
            foreach (var file in files) file.SetStatistics(null, false);
        if (!sameFiles) _files = files.ToArray();
        _context = context;
        _authorizationRevision = authorizationRevision;
        _incomplete = incomplete;
        Schedule(false);
    }

    private void Schedule(bool retry)
    {
        CancelPending();
        if (retry)
            foreach (var file in _files.Where(file => file.IsStatisticsUnavailable)) file.SetStatistics(null, false);
        _isBusy = !_disposed && !_stopped && service is not null && _context is not null && _files.Length > 0;
        PublishTotals();
        if (!_isBusy) return;
        var owner = new CancellationTokenSource();
        lock (_gate) _cancellation = owner;
        _work = RunAsync(_work, owner, _files, _context!);
    }

    private async Task RunAsync(Task previous, CancellationTokenSource owner,
        WorkspaceFileChangeViewModel[] files, ToolExecutionContext context)
    {
        try
        {
            await previous.ConfigureAwait(false);
            await Task.Delay(120, owner.Token).ConfigureAwait(false);
            foreach (var file in files)
            {
                owner.Token.ThrowIfCancellationRequested();
                if (file.IsStatisticsResolved) continue;
                var statistics = await service!.ReadAsync(file.Change, context, owner.Token).ConfigureAwait(false);
                await PublishAsync(owner, () => { file.SetStatistics(statistics, true); PublishTotals(); });
            }
            await PublishAsync(owner, () => { _isBusy = false; PublishTotals(); });
        }
        catch (OperationCanceledException) when (owner.IsCancellationRequested) { }
        catch (Exception)
        {
            await PublishAsync(owner, () =>
            {
                foreach (var file in files.Where(file => !file.IsStatisticsResolved)) file.SetStatistics(null, true);
                _isBusy = false;
                PublishTotals();
            });
        }
        finally
        {
            lock (_gate)
            {
                if (_cancellation == owner) _cancellation = null;
                owner.Dispose();
            }
        }
    }

    private async Task PublishAsync(CancellationTokenSource owner, Action publish)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_disposed && !_stopped && !owner.IsCancellationRequested && _cancellation == owner) publish();
            }, DispatcherPriority.Normal, owner.Token);
        }
        catch (OperationCanceledException) when (owner.IsCancellationRequested) { }
    }

    private void PublishTotals()
    {
        var hasTotals = !_incomplete && _files.Length > 0;
        var hasError = false;
        long added = 0, removed = 0;
        foreach (var file in _files)
        {
            if (file.Statistics is { } statistics)
            {
                added += statistics.Added;
                removed += statistics.Removed;
            }
            else hasTotals = false;
            hasError |= file.IsStatisticsUnavailable;
        }
        var totalsChanged = HasTotals != hasTotals;
        var errorChanged = HasError != hasError;
        var addedChanged = _added != (hasTotals ? added : 0);
        var removedChanged = _removed != (hasTotals ? removed : 0);
        HasTotals = hasTotals;
        HasError = hasError;
        _added = hasTotals ? added : 0;
        _removed = hasTotals ? removed : 0;
        if (totalsChanged) OnPropertyChanged(nameof(HasTotals));
        if (errorChanged) OnPropertyChanged(nameof(HasError));
        if (addedChanged) OnPropertyChanged(nameof(AddedLabel));
        if (removedChanged) OnPropertyChanged(nameof(RemovedLabel));
        _retry?.NotifyCanExecuteChanged();
    }

    public async Task StopAsync()
    {
        _stopped = true;
        CancelPending();
        _retry?.NotifyCanExecuteChanged();
        await _work;
    }

    private void CancelPending()
    {
        lock (_gate) { _cancellation?.Cancel(); _cancellation = null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelPending();
        _retry?.NotifyCanExecuteChanged();
    }
}
