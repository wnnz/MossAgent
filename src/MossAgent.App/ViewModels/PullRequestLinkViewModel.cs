using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestLinkViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly SourcePullRequestLinkResolver _resolver;
    private readonly Func<SourcePullRequestTarget, CancellationToken, Task> _open;
    private readonly Func<bool> _canAct;
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _operation;
    private SourceAccountItemViewModel? _account;
    private string _url = string.Empty;
    private string _status = string.Empty;
    private bool _isOpen;
    private bool _busy;
    private bool _disposed;

    public PullRequestLinkViewModel(SourcePullRequestLinkResolver resolver, ObservableCollection<SourceAccountItemViewModel> accounts,
        Func<SourcePullRequestTarget, CancellationToken, Task> open, Func<bool> canAct)
    {
        _resolver = resolver; Accounts = accounts; _open = open; _canAct = canAct;
        ShowCommand = new RelayCommand(Show, CanAct);
        CloseCommand = new RelayCommand(Close, () => !_disposed && IsOpen);
        CancelCommand = new RelayCommand(Cancel, () => !_disposed && IsBusy);
        OpenCommand = new GuardedAsyncRelayCommand(OpenAsync,
            () => CanAct() && IsOpen && Account is not null && !string.IsNullOrWhiteSpace(Url));
    }

    public ObservableCollection<SourceAccountItemViewModel> Accounts { get; }
    public IRelayCommand ShowCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public GuardedAsyncRelayCommand OpenCommand { get; }
    public bool IsOpen { get => _isOpen; private set { SetProperty(ref _isOpen, value); NotifyAvailability(); } }
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); NotifyAvailability(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Url { get => _url; set { SetProperty(ref _url, value); NotifyAvailability(); } }
    public SourceAccountItemViewModel? Account { get => _account; set { SetProperty(ref _account, value); NotifyAvailability(); } }

    private bool CanAct() => !_disposed && !IsBusy && _canAct();
    private void Show()
    {
        if (IsOpen) return;
        var previous = Account;
        Account = Accounts.FirstOrDefault(account => account.Id == previous?.Id && account.Revision == previous?.Revision);
        Status = string.Empty; IsOpen = true;
    }

    public void Cancel() { if (!_disposed) _operation?.Cancel(); }
    public void Close() { Cancel(); IsOpen = false; }

    private async Task OpenAsync(CancellationToken caller)
    {
        if (!CanAct() || !IsOpen || Account is not { } account) return;
        var url = Url.Trim();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller, _shutdown.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(40));
        _operation = operation; IsBusy = true; Status = PullRequestText.Get("Loading");
        try
        {
            var target = await Task.Run(() => _resolver.ResolveAsync(account.Id, account.Revision, url, operation.Token), operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            await _open(target, operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            await PublishAsync(() => { Status = PullRequestText.Get("LinkLoaded"); IsOpen = false; }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { await SetFailureAsync("Cancelled").ConfigureAwait(false); }
        catch (SourceReviewException exception)
        { await PublishAsync(() => Status = PullRequestText.Failure(exception.ErrorCode)).ConfigureAwait(false); }
        catch (Exception) { await SetFailureAsync("Failed").ConfigureAwait(false); }
        finally { await PublishAsync(() => { _operation = null; IsBusy = false; }).ConfigureAwait(false); }
    }

    private Task SetFailureAsync(string key) => PublishAsync(() => Status = PullRequestText.Get(key));
    private Task PublishAsync(Action action) => AvaloniaUiThread.RunAsync(() => { if (!_disposed) action(); }, CancellationToken.None);

    public void NotifyAvailability()
    {
        ShowCommand?.NotifyCanExecuteChanged(); CloseCommand?.NotifyCanExecuteChanged();
        CancelCommand?.NotifyCanExecuteChanged(); OpenCommand?.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _shutdown.Cancel(); Cancel(); OpenCommand.Dispose(); NotifyAvailability();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (OpenCommand.ExecutionTask is { } work) await work.ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
