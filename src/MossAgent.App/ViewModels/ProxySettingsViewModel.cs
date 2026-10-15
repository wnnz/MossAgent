using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Networking;
using MossAgent.Application.Persistence;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ProxySettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private readonly IConfigurationRepository _repository;
    private readonly IProxyConnectionTester _tester;
    private readonly SettingsMutationRunner _mutations = new();
    private readonly SettingsOperationLifetime _operations;
    private ProxyProfile? _selectedItem;
    private Guid _newProxyId = Guid.NewGuid();
    private int _loadVersion;
    private bool _publishingItems;
    private bool _catalogAvailable;
    private bool _isLoading;
    private bool _disposed;
    private string _name = string.Empty;
    private string _host = "127.0.0.1";
    private int _port = 7890;
    private ProxyProtocol _protocol = ProxyProtocol.Http;
    private bool _isDefault;
    private bool _isEnabled = true;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _status = string.Empty;
    private bool _hasStatusError;
    private bool _isFormOpen;

    public ProxySettingsViewModel(IConfigurationRepository repository,
        IProxyConnectionTester tester, TimeSpan? operationTimeout = null)
        : base(nameof(Name), nameof(Host), nameof(Port), nameof(Protocol), nameof(IsDefault),
            nameof(IsEnabled), nameof(Username), nameof(Password))
    {
        _repository = repository;
        _tester = tester;
        _operations = new SettingsOperationLifetime(operationTimeout);
        Items.CollectionChanged += HandleItemsChanged;
        _mutations.PropertyChanged += HandleMutationChanged;
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, CanMutate);
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync, () => !_disposed && !IsLoading && !IsBusy);
        TestCommand = new AsyncRelayCommand<ProxyProfile?>(TestAsync, CanTest);
        DeleteCommand = new GuardedAsyncRelayCommand(DeleteAsync, () => CanMutate() && SelectedItem is not null);
        NewCommand = new RelayCommand(ResetEditor, () => !_disposed);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; }, () => !_disposed);
        OpenEditFormCommand = new RelayCommand<ProxyProfile?>(OpenEditor, CanEdit);
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false, () => !_disposed);
        _ = ReadAsync();
    }

    public ObservableCollection<ProxyProfile> Items { get; } = [];
    public bool HasNoItems => Items.Count == 0;
    public bool IsBusy => _mutations.IsRunning;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsEditorTargetUnavailable => _catalogAvailable && !IsLoading && SelectedItem is { } selected
        && FindProxy(selected.Id) is null;
    public bool HasEditorNotice => IsEditorTargetUnavailable || (!_catalogAvailable && !IsLoading);
    public string EditorNotice => SettingsText.Get(IsEditorTargetUnavailable ? "ProxyTargetUnavailable" : "ProxyLoadFailed");
    public string SaveLabel => SettingsText.Get(IsBusy ? "ProxySaving" : "ProxySaveLabel");
    public IReadOnlyList<ProxyProtocol> Protocols { get; } = Enum.GetValues<ProxyProtocol>();
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<ProxyProfile?> TestCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand<ProxyProfile?> OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public event Func<Task>? ProxiesChanged;

    public bool IsFormOpen
    {
        get => _isFormOpen;
        set { if (SetProperty(ref _isFormOpen, value)) InvalidateEditor(); }
    }

    public ProxyProfile? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_publishingItems || _disposed || !SetProperty(ref _selectedItem, value)) return;
            InvalidateEditor();
            if (value is not null) LoadEditor(value);
            else _newProxyId = Guid.NewGuid();
            NotifyCommands();
        }
    }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Host { get => _host; set => SetProperty(ref _host, value); }
    public int Port { get => _port; set => SetProperty(ref _port, value); }
    public ProxyProtocol Protocol { get => _protocol; set => SetProperty(ref _protocol, value); }
    public bool IsDefault { get => _isDefault; set => SetProperty(ref _isDefault, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    private Guid EditorId => SelectedItem?.Id ?? _newProxyId;

    private async Task<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        var version = Interlocked.Increment(ref _loadVersion);
        var revision = EditorRevision;
        using var operation = _operations.CreateOperation(cancellationToken);
        IsLoading = true;
        _catalogAvailable = false;
        NotifyCommands();
        try
        {
            var proxies = await _repository.GetProxiesAsync(operation.Token).WaitAsync(operation.Token);
            if (_disposed || operation.IsCancellationRequested || version != _loadVersion) return false;
            _publishingItems = true;
            try { if (!Items.SequenceEqual(proxies)) Items.ReplaceWith(proxies); }
            finally { _publishingItems = false; }
            if (FindProxy(SelectedItem?.Id) is { } selected)
                SetProxyReference(selected);
            _catalogAvailable = true;
            if (revision == EditorRevision && Status == SettingsText.Get("ProxyLoadFailed")) ClearStatus();
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            ReportOperationCancellation(cancellationToken, revision);
            return false;
        }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision) SetStatus("ProxyLoadFailed", true);
            return false;
        }
        finally
        {
            if (!_disposed && version == _loadVersion) { IsLoading = false; NotifyCommands(); }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_disposed || IsLoading || IsBusy) return;
        var revision = EditorRevision;
        using var operation = _operations.CreateOperation(cancellationToken);
        if (await ReadAsync(operation.Token)) await NotifyChangedAsync(operation.Token, revision);
        if (operation.IsCancellationRequested) ReportOperationCancellation(cancellationToken, revision);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate()) return;
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535)
        {
            SetStatus("ProxyRequiredFields", true);
            return;
        }
        var proxy = new ProxyProfile(EditorId, Name.Trim(), Protocol, Host.Trim(), Port,
            EmptyToNull(Username), EmptyToNull(Password), IsDefault, IsEnabled);
        if (!proxy.TryGetUri(out _)) { SetStatus("ProxyInvalidHost", true); return; }
        var editing = SelectedItem is not null;
        var revision = EditorRevision;
        await RunMutationAsync(async token =>
        {
            if (!await ReadAsync(token)) return;
            if (editing && FindProxy(proxy.Id) is null)
            {
                if (revision == EditorRevision) SetStatus("ProxyTargetUnavailable", true);
                return;
            }
            await _repository.SaveProxyAsync(proxy, token);
            if (_disposed || token.IsCancellationRequested) return;
            if (!await ReadAsync(token)) return;
            if (EditorId == proxy.Id && ResolveTarget(proxy) is { } saved) SetProxyReference(saved);
            if (!await NotifyChangedAsync(token, revision)) return;
            if (revision == EditorRevision)
            {
                IsFormOpen = false;
                ResetEditor();
                SetStatus(editing ? "ProxyUpdated" : "ProxyAdded", false);
            }
            else if (EditorId == proxy.Id) SetStatus("ProxyPreviousDraftSaved", false);
        }, cancellationToken, revision, "ProxySaveFailed");
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanMutate() || SelectedItem is not { } proxy) return;
        var revision = EditorRevision;
        await RunMutationAsync(async token =>
        {
            if (!await ReadAsync(token)) return;
            if (FindProxy(proxy.Id) is null)
            {
                if (revision == EditorRevision) SetStatus("ProxyTargetUnavailable", true);
                return;
            }
            try { await _repository.DeleteProxyAsync(proxy.Id, token); }
            catch (ProxyInUseException)
            {
                if (!_disposed && revision == EditorRevision) SetStatus("ProxyDeleteInUse", true);
                return;
            }
            if (_disposed || token.IsCancellationRequested) return;
            if (!await ReadAsync(token) || !await NotifyChangedAsync(token, revision)) return;
            if (revision == EditorRevision)
            {
                IsFormOpen = false;
                ResetEditor();
                SetStatus("ProxyDeleted", false);
            }
        }, cancellationToken, revision, "ProxyDeleteFailed");
    }

    private async Task TestAsync(ProxyProfile? target, CancellationToken cancellationToken)
    {
        if (!CanTest(target) || ResolveTarget(target) is not { } proxy) return;
        var revision = EditorRevision;
        using var operation = _operations.CreateOperation(cancellationToken);
        SetStatus("ProxyTestRunning", false);
        try
        {
            var result = await _tester.TestAsync(proxy, operation.Token).WaitAsync(operation.Token);
            if (!_disposed && revision == EditorRevision)
            {
                HasStatusError = !result.IsSuccess;
                Status = string.Format(SettingsText.Get("ProxyTestResult"), result.Message, result.Elapsed.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_disposed && revision == EditorRevision) SetStatus("ProxyTestCancelled", true);
        }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision) SetStatus("ProxyTestFailed", true);
        }
    }

    private async Task<bool> NotifyChangedAsync(CancellationToken token, long revision)
    {
        try
        {
            if (ProxiesChanged is { } changed)
            {
                foreach (Func<Task> subscriber in changed.GetInvocationList())
                    await subscriber().WaitAsync(token);
            }
            if (!_disposed && revision == EditorRevision && Status == SettingsText.Get("ProxyNotificationFailed")) ClearStatus();
            return !_disposed && !token.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision) SetStatus("ProxyNotificationFailed", true);
            return false;
        }
    }

    private async Task RunMutationAsync(Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken, long revision, string failureKey)
    {
        using var lifetime = _operations.CreateOperation(cancellationToken);
        ClearStatus();
        await _mutations.TryExecuteAsync(operation, lifetime.Token,
            () => { if (!_disposed && revision == EditorRevision) SetStatus(failureKey, true); });
        if (lifetime.IsCancellationRequested) ReportOperationCancellation(cancellationToken, revision);
    }

    private void ReportOperationCancellation(CancellationToken owner, long revision)
    {
        if (!_disposed && revision == EditorRevision)
            SetStatus(owner.IsCancellationRequested ? "ProxyOperationCancelled" : "ProxyOperationTimedOut", true);
    }

    private ProxyProfile? FindProxy(Guid? id)
    {
        if (id is null) return null;
        for (var index = 0; index < Items.Count; index++)
        {
            var proxy = Items[index];
            if (proxy.Id == id) return proxy;
        }
        return null;
    }

    private ProxyProfile? ResolveTarget(ProxyProfile? target) => FindProxy((target ?? SelectedItem)?.Id);
    private bool CanEdit(ProxyProfile? target) => !_disposed && _catalogAvailable && ResolveTarget(target) is not null;
    private bool CanTest(ProxyProfile? target) => CanMutate() && ResolveTarget(target) is { IsEnabled: true };
    private bool CanMutate() => !_disposed && _catalogAvailable && !IsLoading && !IsBusy && !IsEditorTargetUnavailable;

    private void OpenEditor(ProxyProfile? target)
    {
        if (!CanEdit(target) || ResolveTarget(target) is not { } proxy) return;
        InvalidateEditor();
        SetProxyReference(proxy);
        LoadEditor(proxy);
        ClearStatus();
        IsFormOpen = true;
    }

    private void SetProxyReference(ProxyProfile? proxy)
    {
        _selectedItem = proxy;
        OnPropertyChanged(nameof(SelectedItem));
        NotifyCommands();
    }

    private void LoadEditor(ProxyProfile proxy)
    {
        Name = proxy.Name;
        Host = proxy.Host;
        Port = proxy.Port;
        Protocol = proxy.Protocol;
        Username = proxy.Username ?? string.Empty;
        Password = proxy.Password ?? string.Empty;
        IsDefault = proxy.IsDefault;
        IsEnabled = proxy.IsEnabled;
    }

    private void ResetEditor()
    {
        if (_disposed) return;
        InvalidateEditor();
        SetProxyReference(null);
        _newProxyId = Guid.NewGuid();
        Name = string.Empty;
        Host = "127.0.0.1";
        Port = 7890;
        Protocol = ProxyProtocol.Http;
        Username = string.Empty;
        Password = string.Empty;
        IsDefault = false;
        IsEnabled = true;
        ClearStatus();
    }

    private void SetStatus(string key, bool error) { HasStatusError = error; Status = SettingsText.Get(key); }
    private void ClearStatus() { HasStatusError = false; Status = string.Empty; }
    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) => OnPropertyChanged(nameof(HasNoItems));
    private void HandleMutationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(SaveLabel));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        TestCommand.NotifyCanExecuteChanged();
        OpenEditFormCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsEditorTargetUnavailable));
        OnPropertyChanged(nameof(HasEditorNotice));
        OnPropertyChanged(nameof(EditorNotice));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operations.Dispose();
        Items.CollectionChanged -= HandleItemsChanged;
        _mutations.PropertyChanged -= HandleMutationChanged;
        ((IDisposable)SaveCommand).Dispose();
        ((IDisposable)DeleteCommand).Dispose();
        ((IDisposable)RefreshCommand).Dispose();
        TestCommand.Cancel();
        NewCommand.NotifyCanExecuteChanged();
        OpenCreateFormCommand.NotifyCanExecuteChanged();
        CloseFormCommand.NotifyCanExecuteChanged();
        NotifyCommands();
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
