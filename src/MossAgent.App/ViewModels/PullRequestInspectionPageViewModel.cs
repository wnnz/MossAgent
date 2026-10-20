using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestInspectionPageViewModel<T> : ObservableObject, IAsyncDisposable
{
    private readonly Func<int, CancellationToken, Task<PullRequestInspectionPage<T>>> _read;
    private readonly Action<string> _invalidate;
    private CancellationTokenSource? _operation;
    private T? _selectedItem;
    private bool _enabled;
    private bool _busy;
    private bool _disposed;
    private bool _loaded;
    private bool _next;
    private bool _limited;
    private int _page = 1;
    private int _generation;
    private string _status = string.Empty;

    public PullRequestInspectionPageViewModel(Func<int, CancellationToken, Task<PullRequestInspectionPage<T>>> read, Action<string> invalidate)
    {
        _read = read; _invalidate = invalidate;
        LoadCommand = new(token => LoadAsync(1, token), CanRead);
        PreviousCommand = new(token => LoadAsync(_page - 1, token), () => CanRead() && _page > 1);
        NextCommand = new(token => LoadAsync(_page + 1, token), () => CanRead() && _next);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => _busy);
    }

    public ObservableCollection<T> Items { get; } = [];
    public GuardedAsyncRelayCommand LoadCommand { get; }
    public GuardedAsyncRelayCommand PreviousCommand { get; }
    public GuardedAsyncRelayCommand NextCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public T? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public string PageLabel => string.Format(CultureInfo.CurrentCulture, PullRequestText.Get("PageLabel"), _page, Items.Count);
    public int Page => _page;
    public bool IsBusy => _busy;
    public bool IsLimited => _limited;
    public bool IsEmpty => _loaded && !_busy && Items.Count == 0;
    private bool CanRead() => _enabled && !_busy && !_disposed;

    public void Activate() { _enabled = true; Notify(); }

    public void Clear()
    {
        _generation++; _enabled = false; _operation?.Cancel();
        Items.Clear(); SelectedItem = default; _page = 1; _next = _limited = _loaded = false;
        Status = string.Empty; Notify();
    }

    private async Task LoadAsync(int page, CancellationToken caller)
    {
        if (!CanRead()) return;
        var generation = _generation;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller);
        operation.CancelAfter(TimeSpan.FromSeconds(40));
        _operation = operation; _busy = true; _loaded = false; _limited = false;
        Items.Clear(); SelectedItem = default; Status = PullRequestText.Get("Loading"); Notify();
        try
        {
            var result = await _read(page, operation.Token).ConfigureAwait(false);
            await PublishAsync(() =>
            {
                if (operation.IsCancellationRequested || generation != _generation) return;
                foreach (var item in result.Items) Items.Add(item);
                _page = result.Page; _next = result.HasNextPage; _limited = result.IsLimited; _loaded = true;
                SelectedItem = Items.FirstOrDefault(); Status = PullRequestText.Get("Loaded");
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { await ReportAsync(generation, "tool_cancelled").ConfigureAwait(false); }
        catch (SourceReviewException exception)
        { await ReportAsync(generation, exception.ErrorCode).ConfigureAwait(false); }
        catch (Exception)
        { await ReportAsync(generation, "source_unavailable").ConfigureAwait(false); }
        finally
        {
            _operation = null;
            await PublishAsync(() => { _busy = false; Notify(); }).ConfigureAwait(false);
        }
    }

    private Task ReportAsync(int generation, string code) => PublishAsync(() =>
    {
        if (generation != _generation) return;
        if (code is "source_revision_changed" or "source_scope_denied" or "source_auth_failed") _invalidate(code);
        else Status = PullRequestText.Failure(code);
    });

    private Task PublishAsync(Action action) => AvaloniaUiThread.RunAsync(() => { if (!_disposed) action(); }, CancellationToken.None);

    private void Notify()
    {
        LoadCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Page)); OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(IsLimited)); OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(PageLabel));
    }

    public async Task DrainAsync()
    {
        foreach (var command in new[] { LoadCommand, PreviousCommand, NextCommand })
            if (command.ExecutionTask is { } work) await work.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear(); _disposed = true;
        LoadCommand.Dispose(); PreviousCommand.Dispose(); NextCommand.Dispose();
    }

    public async ValueTask DisposeAsync() { Dispose(); await DrainAsync().ConfigureAwait(false); }
}
