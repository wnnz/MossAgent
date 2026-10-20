using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class SourceAccountSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISourceAccountRepository _repository;
    private readonly IConfigurationRepository _configurations;
    private readonly SourceAccountConnections _connections;
    private readonly SettingsOperationLifetime _operations;
    private bool _disposed;
    private bool _isBusy;
    private bool _hasStatusError;
    private string _status = string.Empty;
    private long _proxyLoadVersion;

    public SourceAccountSettingsViewModel(ISourceAccountRepository repository,
        IConfigurationRepository configurations, SourceAccountConnections connections, TimeSpan? operationTimeout = null)
    {
        _repository = repository;
        _configurations = configurations;
        _connections = connections;
        _operations = new SettingsOperationLifetime(operationTimeout);
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync, CanAct);
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, () => CanAct() && Editor.IsOpen);
        DeleteCommand = new GuardedAsyncRelayCommand(DeleteAsync, () => CanAct() && Editor.IsOpen && Editor.StoredRevision > 0);
        VerifyCommand = new AsyncRelayCommand<SourceAccountItemViewModel>(VerifyAsync,
            item => CanAct() && item is { IsEnabled: true });
        OpenEditFormCommand = new AsyncRelayCommand<SourceAccountItemViewModel>(OpenEditorAsync,
            item => CanAct() && item is not null);
        OpenCreateFormCommand = new RelayCommand(() => { if (!_disposed) { Editor.Open(null); NotifyCommands(); } });
        CloseFormCommand = new RelayCommand(() => { Editor.Close(); NotifyCommands(); });
        Initialization = RefreshAsync(_operations.Token);
    }

    public SourceAccountEditorViewModel Editor { get; } = new();
    public ObservableCollection<SourceAccountItemViewModel> Items { get; } = [];
    public Task Initialization { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand<SourceAccountItemViewModel> VerifyCommand { get; }
    public IAsyncRelayCommand<SourceAccountItemViewModel> OpenEditFormCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public bool HasNoItems => Items.Count == 0;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(Status)); }
    }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public string Status
    {
        get => IsBusy && _status.Length == 0 ? SourceAccountText.Get("Working") : _status;
        private set => SetProperty(ref _status, value);
    }
    private bool CanAct() => !_disposed && !IsBusy;

    private Task RefreshAsync(CancellationToken cancellationToken) => RunAsync(async token =>
    {
        await ReloadProxiesAsync(token);
        await ReadAccountsAsync(token);
        if (!_disposed) SetStatus("Loaded");
    }, cancellationToken);

    public async Task ReloadProxiesAsync()
    {
        try { await ReloadProxiesAsync(_operations.Token); }
        catch (SourceConnectionException) { }
        catch (OperationCanceledException) { }
    }

    private async Task ReloadProxiesAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        var version = ++_proxyLoadVersion;
        using var operation = _operations.CreateOperation(cancellationToken);
        try
        {
            var proxies = await _configurations.GetProxiesAsync(operation.Token);
            if (!_disposed && !operation.IsCancellationRequested && version == _proxyLoadVersion)
                Editor.PublishProxies(proxies);
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (_disposed || version != _proxyLoadVersion) return;
            Editor.InvalidateProxies();
            SetStatus("ProxyUnavailable", true);
            throw new SourceConnectionException(SourceConnectionFailure.ProxyUnavailable);
        }
    }

    private async Task ReadAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await _repository.GetAllAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed) return;
        Items.ReplaceWith(accounts.Select(account => new SourceAccountItemViewModel(account)));
        OnPropertyChanged(nameof(HasNoItems));
    }

    private Task OpenEditorAsync(SourceAccountItemViewModel? item) => RunAsync(async token =>
    {
        if (item is null) return;
        var stamp = Editor.RevisionStamp;
        var account = await _repository.GetAsync(item.Id, token);
        token.ThrowIfCancellationRequested();
        if (account is null) throw new SourceConnectionException(SourceConnectionFailure.AccountUnavailable);
        if (!_disposed && stamp == Editor.RevisionStamp) Editor.Open(account);
    }, _operations.Token);

    private Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanAct() || !Editor.IsOpen) return Task.CompletedTask;
        SourceAccount account;
        try { account = Editor.Capture(); }
        catch (SourceConnectionException exception) { SetStatus(exception.Failure.ToString(), true); return Task.CompletedTask; }
        catch (ArgumentException) { SetStatus("InvalidDraft", true); return Task.CompletedTask; }
        var stamp = Editor.RevisionStamp;
        return RunAsync(async token =>
        {
            await _repository.SaveAsync(account, token);
            await ReadAccountsAsync(token);
            if (_disposed) return;
            var unchanged = stamp == Editor.RevisionStamp;
            if (unchanged) Editor.Close();
            SetStatus(unchanged ? "Saved" : "PreviousSaved");
        }, cancellationToken);
    }

    private Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanAct() || !Editor.IsOpen || Editor.StoredRevision == 0) return Task.CompletedTask;
        var id = Editor.Id;
        var revision = Editor.StoredRevision;
        var stamp = Editor.RevisionStamp;
        return RunAsync(async token =>
        {
            await _repository.DeleteAsync(id, revision, token);
            await ReadAccountsAsync(token);
            if (_disposed) return;
            if (stamp == Editor.RevisionStamp) Editor.Close();
            SetStatus("Deleted");
        }, cancellationToken);
    }

    private Task VerifyAsync(SourceAccountItemViewModel? item, CancellationToken cancellationToken) => RunAsync(async token =>
    {
        if (item is null) return;
        item.SetFailure(SourceAccountText.Get("Unverified"));
        try
        {
            var identity = await _connections.RefreshIdentityAsync(item.Id, item.Revision, token);
            if (_disposed || !Items.Contains(item)) return;
            item.SetIdentity(identity);
            Status = item.Status;
        }
        catch (SourceConnectionException exception)
        {
            if (!_disposed && Items.Contains(item)) item.SetFailure(SourceAccountText.Get(exception.Failure.ToString()));
            throw;
        }
    }, cancellationToken);

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (!CanAct()) return;
        using var operation = _operations.CreateOperation(cancellationToken);
        IsBusy = true;
        Status = string.Empty;
        HasStatusError = false;
        NotifyCommands();
        try { await action(operation.Token); }
        catch (OperationCanceledException)
        {
            if (!_disposed) SetStatus(cancellationToken.IsCancellationRequested ? "Cancelled" : "TimedOut", true);
        }
        catch (SourceConnectionException exception)
        {
            if (!_disposed) SetStatus(exception.Failure.ToString(), true);
        }
        catch (Exception) { if (!_disposed) SetStatus("OperationFailed", true); }
        finally
        {
            if (!_disposed) { IsBusy = false; NotifyCommands(); }
        }
    }

    private void SetStatus(string key, bool error = false)
    {
        Status = SourceAccountText.Get(key);
        HasStatusError = error;
    }

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        VerifyCommand.NotifyCanExecuteChanged();
        OpenEditFormCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operations.Dispose();
        Editor.Close();
        NotifyCommands();
    }
}
