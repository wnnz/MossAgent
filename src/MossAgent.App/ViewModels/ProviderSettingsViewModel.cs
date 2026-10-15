using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Resources;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ProviderSettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.ProviderSettingsStrings", typeof(ProviderSettingsViewModel).Assembly);
    private readonly IConfigurationRepository _repository;
    private AiProvider? _selectedItem;
    private Guid _newProviderId = Guid.NewGuid();
    private string _name = string.Empty;
    private string _baseUrl = "https://api.openai.com/v1/";
    private string _apiKey = string.Empty;
    private ProviderProtocol _protocol = ProviderProtocol.OpenAiResponses;
    private bool _isDefault;
    private bool _isEnabled = true;
    private ProxySelection? _selectedProxy;
    private Guid? _defaultProxyId;
    private Guid? _desiredProxyId;
    private bool _hasProxyChoice;
    private bool _isReplacingProxies;
    private bool _hasProxyValidationError;
    private int _proxyLoadVersion;
    private string _headersJson = "{}";
    private string _status = string.Empty;
    private bool _hasValidationError;
    private Guid? _deleteConfirmationId;
    private bool _isFormOpen;
    private bool _disposed;

    public ProviderSettingsViewModel(IConfigurationRepository repository)
        : this(repository, TimeSpan.FromSeconds(30)) { }

    internal ProviderSettingsViewModel(IConfigurationRepository repository, TimeSpan readTimeout)
        : base(nameof(SelectedItem), nameof(IsFormOpen), nameof(Name), nameof(BaseUrl), nameof(ApiKey),
            nameof(Protocol), nameof(SelectedProxy), nameof(HeadersJson), nameof(IsDefault), nameof(IsEnabled))
    {
        _repository = repository;
        Catalog = new ProviderModelCatalogViewModel(repository, readTimeout);
        Catalog.SnapshotChanged += SynchronizeSelection;
        Catalog.PropertyChanged += HandleCatalogStateChanged;
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_disposed && !Catalog.IsLoading && !Catalog.Mutations.IsRunning);
        NewCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); Status = string.Empty; IsFormOpen = true; });
        OpenEditFormCommand = new RelayCommand(() => EditProvider(SelectedItem));
        CloseFormCommand = new RelayCommand(() => { IsFormOpen = false; CancelDeleteConfirmation(); });
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, HasSelection);
        EditProviderCommand = new RelayCommand<AiProvider>(EditProvider, CanEditProvider);
        _ = LoadAsync();
    }

    public ProviderModelCatalogViewModel Catalog { get; }
    public ObservableCollection<AiProvider> Items => Catalog.Providers;
    public ObservableCollection<ProxySelection> Proxies { get; } = [];
    public IReadOnlyList<ProviderProtocolOption> ProtocolOptions { get; } = ProviderProtocolOption.All;
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public IRelayCommand<AiProvider> EditProviderCommand { get; }

    public event Func<Task>? ProvidersChanged;
    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// 是否弹出供应商配置表单弹窗。
    /// </summary>
    public bool IsFormOpen
    {
        get => _isFormOpen;
        set => SetProperty(ref _isFormOpen, value);
    }

    public AiProvider? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value is not null)
            {
                LoadEditor(value);
            }

            NotifySelectionChanged();
        }
    }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    public string ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }
    public ProviderProtocol Protocol
    {
        get => _protocol;
        set
        {
            if (SetProperty(ref _protocol, value))
            {
                OnPropertyChanged(nameof(SelectedProtocolOption));
            }
        }
    }

    public ProviderProtocolOption SelectedProtocolOption
    {
        get => ProviderProtocolOption.FromProtocol(Protocol);
        set
        {
            if (value is not null)
            {
                Protocol = value.Protocol;
            }
        }
    }
    public bool IsDefault { get => _isDefault; set => SetProperty(ref _isDefault, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public ProxySelection? SelectedProxy
    {
        get => _selectedProxy;
        set
        {
            if (!_isReplacingProxies && value is not null)
            {
                _desiredProxyId = value.Id;
                _hasProxyChoice = true;
                if (_hasProxyValidationError) Status = string.Empty;
            }
            SetProperty(ref _selectedProxy, value);
        }
    }
    public string HeadersJson { get => _headersJson; set => SetProperty(ref _headersJson, value); }
    public string Status
    {
        get => _status;
        private set
        {
            SetProperty(ref _status, value);
            HasValidationError = false;
            _hasProxyValidationError = false;
        }
    }
    public bool HasValidationError
    {
        get => _hasValidationError;
        private set => SetProperty(ref _hasValidationError, value);
    }
    public string EditorTitle => SelectedItem is null ? "添加 AI 提供商" : "编辑 AI 提供商";
    public string ApiKeyPlaceholder => Labels.GetString(
        SelectedItem is null ? "NewApiKeyPlaceholder" : "EditApiKeyPlaceholder")
        ?? throw new InvalidOperationException("Missing provider API Key placeholder resource.");
    public bool HasSelectedProvider => SelectedItem is not null;
    public bool IsEditorTargetUnavailable => SelectedItem is { } selected && Catalog.FindProvider(selected.Id) is null;
    public string EditorNotice => IsEditorTargetUnavailable ? ProxyLabel("ProviderTargetUnavailable") : Catalog.Notice;
    public bool HasEditorNotice => IsEditorTargetUnavailable || Catalog.HasNotice;
    public string DeleteButtonText => SelectedItem is not null && _deleteConfirmationId == SelectedItem.Id
        ? "确认删除"
        : "删除";

    private async Task<bool> LoadAsync()
    {
        if (_disposed || Catalog.Mutations.IsRunning) return false;
        var loaded = await Catalog.RefreshAsync();
        if (loaded && !_disposed) await ReloadProxiesAsync();
        return loaded && !_disposed;
    }

    public async Task ReloadProxiesAsync()
    {
        if (_disposed) return;
        var version = Interlocked.Increment(ref _proxyLoadVersion);
        try
        {
            using var read = Catalog.CreateReadOperation();
            var proxies = await _repository.GetProxiesAsync(read.Token).WaitAsync(read.Token);
            if (_disposed || version != Volatile.Read(ref _proxyLoadVersion)) return;
            read.Token.ThrowIfCancellationRequested();
            _defaultProxyId = proxies.FirstOrDefault(proxy => proxy.IsDefault && proxy.IsEnabled)?.Id;
            var desiredId = _hasProxyChoice ? _desiredProxyId : _defaultProxyId;
            _isReplacingProxies = true;
            try
            {
                Proxies.ReplaceWith(ProxySelection.FromProfiles(proxies, ProxyLabel("DirectConnection")));
                SelectProxy(desiredId);
            }
            finally
            {
                _isReplacingProxies = false;
            }
            if (SelectedProxy is null) SetProxyValidationError("ProxyUnavailable");
            else if (_hasProxyValidationError) Status = string.Empty;
        }
        catch (Exception)
        {
            if (!_disposed && version == Volatile.Read(ref _proxyLoadVersion)) SetProxyValidationError("ProxyReadFailed");
        }
    }

    private async Task RefreshAsync()
    {
        if (!await LoadAsync()) return;
        if (ProvidersChanged is { } providersChanged)
        {
            await providersChanged();
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave()) return;
        var revision = EditorRevision;
        var selectedId = SelectedItem?.Id;
        if (string.IsNullOrWhiteSpace(Name))
        {
            SetValidationError("请输入提供商名称。");
            return;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            || !IsSupportedBaseUri(uri))
        {
            SetValidationError("请填写有效的 HTTP/HTTPS Base URL，且不要在 URL 中嵌入账号或密码。");
            return;
        }

        Dictionary<string, string> headers;
        try
        {
            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(HeadersJson) ?? [];
        }
        catch (JsonException)
        {
            SetValidationError("额外请求头必须是字符串键值 JSON 对象。");
            return;
        }

        if (!await IsValidSelectedProxyAsync()) return;
        if (!CanSave() || SelectedItem?.Id != selectedId)
        {
            SetValidationError(ProxyLabel("ProviderTargetUnavailable"));
            return;
        }
        if (revision != EditorRevision)
        {
            SetValidationError(ProxyLabel("ProviderDraftChanged"));
            return;
        }

        var provider = new AiProvider(
            SelectedItem?.Id ?? _newProviderId, Name.Trim(), Protocol, uri,
            string.IsNullOrWhiteSpace(ApiKey) && SelectedItem is not null
                ? SelectedItem.ApiKey
                : ApiKey.Trim(),
            SelectedProxy?.Id,
            IsDefault, IsEnabled, headers);
        if (!await Catalog.Mutations.TryExecuteAsync(token => _repository.SaveProviderAsync(provider, token),
            Catalog.LifetimeToken, message => { if (!_disposed && revision == EditorRevision) SetValidationError(message); }, "ProviderSaveFailed"))
        {
            await RefreshAsync();
            return;
        }
        var unchanged = revision == EditorRevision;
        if (SelectedItem is null && _newProviderId == provider.Id)
        {
            SetProperty(ref _selectedItem, provider, nameof(SelectedItem));
            NotifySelectionChanged();
        }
        if (unchanged)
        {
            Status = selectedId is null ? "提供商已添加。" : "提供商设置已更新。";
            IsFormOpen = false;
            ResetEditor();
        }
        await RefreshAsync();
    }

    private async Task DeleteAsync()
    {
        var provider = SelectedItem;
        if (provider is null || !HasSelection())
        {
            return;
        }

        if (_deleteConfirmationId != provider.Id)
        {
            _deleteConfirmationId = provider.Id;
            OnPropertyChanged(nameof(DeleteButtonText));
            Status = "再次点击确认删除；该提供商下的模型配置也会删除。";
            return;
        }

        var revision = EditorRevision;
        if (!await Catalog.Mutations.TryExecuteAsync(token => _repository.DeleteProviderAsync(provider.Id, token),
            Catalog.LifetimeToken, message => { if (!_disposed && revision == EditorRevision) SetValidationError(message); }, "ProviderDeleteFailed")) return;
        if (revision == EditorRevision)
        {
            Status = $"已删除提供商：{provider.Name}";
            IsFormOpen = false;
            ResetEditor();
        }
        await RefreshAsync();
    }

    private void LoadEditor(AiProvider provider)
    {
        Name = provider.Name;
        BaseUrl = provider.BaseUri.ToString();
        ApiKey = string.Empty;
        Protocol = provider.Protocol;
        IsDefault = provider.IsDefault;
        IsEnabled = provider.IsEnabled;
        HeadersJson = JsonSerializer.Serialize(provider.Headers);
        SelectProxy(provider.ProxyId);
        if (SelectedProxy is null)
        {
            SetProxyValidationError("ProxyUnavailable");
        }
        else
        {
            Status = "API Key 留空将保留当前值。";
        }
    }

    private void ResetEditor()
    {
        InvalidateEditor();
        _newProviderId = Guid.NewGuid();
        SelectedItem = null;
        Name = string.Empty;
        BaseUrl = "https://api.openai.com/v1/";
        ApiKey = string.Empty;
        Protocol = ProviderProtocol.OpenAiResponses;
        IsDefault = false;
        IsEnabled = true;
        HeadersJson = "{}";
        SelectProxy(PreferredProxy()?.Id);
    }

    private void NotifySelectionChanged()
    {
        CancelDeleteConfirmation();
        DeleteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(HasSelectedProvider));
        OnPropertyChanged(nameof(ApiKeyPlaceholder));
        HandleCatalogStateChanged(this, new PropertyChangedEventArgs(null));
    }

    private bool HasSelection() => CanSave() && SelectedItem is not null;
    private bool CanSave() => !_disposed && Catalog.CanMutate && !IsEditorTargetUnavailable;

    private ProxySelection? PreferredProxy() =>
        Proxies.FirstOrDefault(proxy => proxy.Id == _defaultProxyId)
        ?? Proxies.FirstOrDefault();

    private void SelectProxy(Guid? id)
    {
        _desiredProxyId = id;
        _hasProxyChoice = true;
        SetProperty(ref _selectedProxy, Proxies.FirstOrDefault(proxy => proxy.Id == id), nameof(SelectedProxy));
    }

    private async Task<bool> IsValidSelectedProxyAsync()
    {
        var selected = SelectedProxy;
        if (selected is null)
        {
            SetProxyValidationError("ProxySelectionRequired");
            return false;
        }
        try
        {
            using var read = Catalog.CreateReadOperation();
            var proxies = await _repository.GetProxiesAsync(read.Token).WaitAsync(read.Token);
            if (_disposed) return false;
            read.Token.ThrowIfCancellationRequested();
            if (SelectedProxy is null || SelectedProxy.Id != selected.Id)
            {
                SetProxyValidationError("ProxySelectionChanged");
                return false;
            }
            if (selected.Id is { } proxyId && !proxies.Any(proxy => proxy.Id == proxyId && proxy.IsEnabled))
            {
                SetProxyValidationError("ProxyUnavailable");
                return false;
            }
            return true;
        }
        catch (Exception)
        {
            if (!_disposed) SetProxyValidationError("ProxyReadFailed");
            return false;
        }
    }

    private static string ProxyLabel(string key) => Labels.GetString(key)
        ?? throw new InvalidOperationException("Missing provider proxy resource.");

    private void SetProxyValidationError(string key)
    {
        SetValidationError(ProxyLabel(key));
        _hasProxyValidationError = true;
    }

    private static bool IsSupportedBaseUri(Uri uri) =>
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
         || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrWhiteSpace(uri.UserInfo);

    private void SetValidationError(string message)
    {
        Status = message;
        HasValidationError = true;
    }

    private void EditProvider(AiProvider? provider)
    {
        if (FindEditableProvider(provider) is not { } current)
        {
            return;
        }

        var reload = !IsFormOpen && Equals(SelectedItem, current);
        SelectedItem = current;
        if (reload) LoadEditor(current);
        IsFormOpen = true;
    }

    private bool CanEditProvider(AiProvider? provider) => FindEditableProvider(provider) is not null;
    private AiProvider? FindEditableProvider(AiProvider? provider) => !_disposed && Catalog.CanMutate
        && provider is not null ? Catalog.FindProvider(provider.Id) : null;

    private void CancelDeleteConfirmation()
    {
        if (_deleteConfirmationId is null)
        {
            return;
        }

        _deleteConfirmationId = null;
        OnPropertyChanged(nameof(DeleteButtonText));
    }

    private void SynchronizeSelection()
    {
        var current = Catalog.FindProvider(SelectedItem?.Id ?? _newProviderId);
        if (IsFormOpen)
        {
            var wasNew = SelectedItem is null;
            if (current is not null) SetProperty(ref _selectedItem, current, nameof(SelectedItem));
            if (wasNew && current is not null) NotifySelectionChanged();
        }
        else SelectedItem = current;
        HandleCatalogStateChanged(this, new PropertyChangedEventArgs(null));
    }

    private void HandleCatalogStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        EditProviderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsEditorTargetUnavailable));
        OnPropertyChanged(nameof(EditorNotice));
        OnPropertyChanged(nameof(HasEditorNotice));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _proxyLoadVersion);
        Catalog.SnapshotChanged -= SynchronizeSelection;
        Catalog.PropertyChanged -= HandleCatalogStateChanged;
        Catalog.Dispose();
        ProvidersChanged = null;
        HandleCatalogStateChanged(this, new PropertyChangedEventArgs(null));
    }
}
