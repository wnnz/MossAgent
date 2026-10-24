using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class McpSettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private readonly IMcpConfigurationRepository _configurations;
    private readonly McpSettingsCatalog _catalog;
    private readonly IMcpManager _manager;
    private readonly IToolCatalog _tools;
    private readonly AppStartupViewModel? _startup;
    private string _name = string.Empty;
    private McpTransportKind _transport;
    private string _command = string.Empty;
    private string _argumentsJson = "[]";
    private string _workingDirectory = string.Empty;
    private string _environmentJson = "{}";
    private string _url = string.Empty;
    private string _headersJson = "{}";
    private ProxySelection? _selectedProxy;
    private McpServerProfile? _selectedItem;
    private bool _isEnabled = true;
    private bool _deleteConfirmationPending;
    private string _status = string.Empty;
    private bool _hasStatusError;
    private bool _isFormOpen;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private Guid _editorId = Guid.NewGuid();
    private bool _editingExisting;

    public McpSettingsViewModel(
        IMcpConfigurationRepository configurations,
        IConfigurationRepository proxies,
        IMcpManager manager,
        IToolCatalog tools,
        AppStartupViewModel? startup = null) : base(
            nameof(Name), nameof(Transport), nameof(Command), nameof(ArgumentsJson), nameof(WorkingDirectory),
            nameof(EnvironmentJson), nameof(Url), nameof(HeadersJson), nameof(IsEnabled), nameof(IsFormOpen))
    {
        _configurations = configurations;
        _catalog = new McpSettingsCatalog(configurations, proxies);
        _manager = manager;
        _tools = tools;
        _startup = startup;
        if (_startup is not null) _startup.PropertyChanged += HandleStartupChanged;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoItems));
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, () => CanEdit);
        ReloadCommand = new GuardedAsyncRelayCommand(ReloadFromUiAsync, () => CanEdit && !IsFormOpen);
        CancelReloadCommand = new RelayCommand(ReloadCommand.Cancel);
        NewCommand = new RelayCommand(ResetEditor, () => CanEdit);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; }, () => CanEdit);
        OpenEditFormCommand = new RelayCommand<McpServerProfile>(OpenEditForm, _ => CanEdit);
        CloseFormCommand = new RelayCommand(() => { IsFormOpen = false; CancelDeleteConfirmation(); });
        ToggleEnabledCommand = new GuardedAsyncRelayCommand<McpServerProfile>(ToggleEnabledAsync, _ => CanEdit);
        DeleteCommand = new GuardedAsyncRelayCommand(DeleteAsync, HasSelection);
        foreach (var command in new IAsyncRelayCommand[] { SaveCommand, ReloadCommand, ToggleEnabledCommand, DeleteCommand })
            command.PropertyChanged += HandleOperationChanged;
        _ = LoadSafelyAsync();
    }

    public ObservableCollection<McpServerProfile> Items => _catalog.Items;
    public bool HasNoItems => Items.Count == 0;
    public ObservableCollection<ProxySelection> ProxyOptions => _catalog.ProxyOptions;
    public IReadOnlyList<McpTransportOption> TransportOptions { get; } = McpTransportOption.All;
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand CancelReloadCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand<McpServerProfile> OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public IAsyncRelayCommand<McpServerProfile> ToggleEnabledCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public bool CanEdit => !_disposed && !ReloadCommand.IsRunning && !SaveCommand.IsRunning
        && !ToggleEnabledCommand.IsRunning && !DeleteCommand.IsRunning;

    /// <summary>
    /// 是否弹出 MCP 服务配置表单弹窗。
    /// </summary>
    public bool IsFormOpen
    {
        get => _isFormOpen;
        set
        {
            if (!_disposed && SetProperty(ref _isFormOpen, value)) ReloadCommand.NotifyCanExecuteChanged();
        }
    }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public McpTransportKind Transport
    {
        get => _transport;
        set
        {
            if (!SetProperty(ref _transport, value)) return;
            OnPropertyChanged(nameof(SelectedTransportOption));
            OnPropertyChanged(nameof(IsStdioTransport));
            OnPropertyChanged(nameof(IsHttpTransport));
        }
    }
    public McpTransportOption SelectedTransportOption
    {
        get => McpTransportOption.FromTransport(Transport);
        set
        {
            if (value is not null) Transport = value.Transport;
        }
    }
    public bool IsStdioTransport => Transport == McpTransportKind.Stdio;
    public bool IsHttpTransport => Transport == McpTransportKind.StreamableHttp;
    public string Command { get => _command; set => SetProperty(ref _command, value); }
    public string ArgumentsJson { get => _argumentsJson; set => SetProperty(ref _argumentsJson, value); }
    public string WorkingDirectory { get => _workingDirectory; set => SetProperty(ref _workingDirectory, value); }
    public string EnvironmentJson { get => _environmentJson; set => SetProperty(ref _environmentJson, value); }
    public string Url { get => _url; set => SetProperty(ref _url, value); }
    public string HeadersJson { get => _headersJson; set => SetProperty(ref _headersJson, value); }
    public ProxySelection? SelectedProxy
    {
        get => _selectedProxy;
        set { if (!_disposed && !_catalog.IsPublishing && SetProperty(ref _selectedProxy, value)) InvalidateEditor(); }
    }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public McpServerProfile? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_disposed || _catalog.IsPublishing || Equals(_selectedItem, value)) return;
            InvalidateEditor();
            SetSelectionReference(value);
            CancelDeleteConfirmation();
            LoadSelection(value);
        }
    }

    public string EditorTitle => SelectedItem is null ? UiText.T("添加 MCP Server") : UiText.T("编辑 MCP Server");
    public bool CanDelete => SelectedItem is not null;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public string DeleteButtonText => _deleteConfirmationPending ? UiText.T("确认删除") : UiText.T("删除");
    public string Status
    {
        get => _status;
        private set
        {
            if (!_disposed && SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }

    private async Task LoadAsync(Guid? selectedId = null, CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        var token = cancellationToken.CanBeCanceled ? cancellationToken : _lifetime.Token;
        if (!await _catalog.ReadAsync(token) || _disposed) return;
        var proxyId = SelectedProxy?.Id;
        SetProperty(ref _selectedProxy, ProxyOptions.FirstOrDefault(item => item.Id == proxyId) ?? ProxyOptions[0], nameof(SelectedProxy));
        var target = selectedId == _editorId ? selectedId : SelectedItem?.Id;
        var selected = Items.FirstOrDefault(item => item.Id == target);
        SetSelectionReference(selected ?? (IsFormOpen ? SelectedItem : null));
        if (selected?.Id == _editorId) _editingExisting = true;
    }

    private Task SaveAsync(CancellationToken cancellationToken) => RunMutationAsync(async (revision, token) =>
    {
        if (!McpSettingsProfileFactory.TryCreate(this, _editorId, out var profile, out var error))
        {
            SetStatus(error, true);
            return;
        }
        var editing = _editingExisting;
        var existing = await _configurations.GetAllAsync(token);
        token.ThrowIfCancellationRequested();
        if (_disposed) return;
        if (editing && !existing.Any(item => item.Id == profile.Id))
        {
            ReportStatus(revision, "McpTargetUnavailable");
            return;
        }
        if (existing.Any(item => item.Id != profile.Id
            && string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
        {
            ReportStatus(revision, "McpDuplicateName");
            return;
        }
        await _configurations.SaveAsync(profile, token);
        token.ThrowIfCancellationRequested();
        if (_disposed || !await TryRefreshAsync(profile.Id, revision, token, "McpSavedRefreshFailed")) return;
        if (!OwnsEditor(revision))
        {
            if (_editorId == profile.Id && IsFormOpen) SetStatus(SettingsText.Get("McpPreviousDraftSaved"));
            return;
        }
        var connectionFailed = _manager.Errors.TryGetValue(profile.Name, out var connectionError);
        SetStatus(connectionFailed
            ? UiText.T("已保存 MCP server：{0}，但连接失败：{1}", profile.Name, connectionError)
            : UiText.T("已保存 MCP server：{0}", profile.Name), connectionFailed);
        IsFormOpen = connectionFailed;
    }, cancellationToken, "McpSaveFailed");

    private Task ToggleEnabledAsync(McpServerProfile? selected, CancellationToken cancellationToken) =>
        RunMutationAsync(async (revision, token) =>
    {
        if (selected is null) return;
        var profiles = await _configurations.GetAllAsync(token);
        token.ThrowIfCancellationRequested();
        if (_disposed) return;
        var current = profiles.FirstOrDefault(item => item.Id == selected.Id);
        if (current is null)
        {
            ReportStatus(revision, "McpTargetUnavailable");
            return;
        }
        var updated = current with { IsEnabled = !current.IsEnabled };
        await _configurations.SaveAsync(updated, token);
        token.ThrowIfCancellationRequested();
        if (_disposed || !await TryRefreshAsync(updated.Id, revision, token, "McpToggleRefreshFailed")
            || !OwnsEditor(revision)) return;
        var connectionFailed = updated.IsEnabled && _manager.Errors.ContainsKey(updated.Name);
        SetStatus(!updated.IsEnabled
            ? UiText.T("MCP server 已禁用。")
            : _manager.Errors.TryGetValue(updated.Name, out var error)
                ? UiText.T("MCP server 已启用，但连接失败：{0}", error)
                : UiText.T("MCP server 已启用并连接。"), connectionFailed);
    }, cancellationToken, "McpToggleFailed");

    private Task DeleteAsync(CancellationToken cancellationToken) => RunMutationAsync(async (revision, token) =>
    {
        if (SelectedItem is not { } selected)
        {
            return;
        }

        if (!_deleteConfirmationPending)
        {
            _deleteConfirmationPending = true;
            OnPropertyChanged(nameof(DeleteButtonText));
            SetStatus(UiText.T("再次点击“确认删除”以删除 {0}。", selected.Name));
            return;
        }

        var profiles = await _configurations.GetAllAsync(token);
        token.ThrowIfCancellationRequested();
        if (_disposed) return;
        if (!profiles.Any(item => item.Id == selected.Id))
        {
            ReportStatus(revision, "McpTargetUnavailable");
            return;
        }
        await _configurations.DeleteAsync(selected.Id, token);
        token.ThrowIfCancellationRequested();
        if (_disposed) return;
        if (OwnsEditor(revision))
        {
            ResetEditor();
            IsFormOpen = false;
            revision = EditorRevision;
        }
        if (await TryRefreshAsync(null, revision, token, "McpDeletedRefreshFailed") && OwnsEditor(revision))
            SetStatus(UiText.T("已删除 MCP server：{0}", selected.Name));
    }, cancellationToken, "McpDeleteFailed");

    private async Task<bool> TryRefreshAsync(Guid? selectedId, long revision, CancellationToken token, string failureKey)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            if (_disposed) return false;
            await _manager.ReloadAsync(token);
            token.ThrowIfCancellationRequested();
            if (_disposed) return false;
            await LoadAsync(selectedId, token);
            return !_disposed && !token.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            ReportStatus(revision, failureKey);
            return false;
        }
    }

    private async Task ReloadFromUiAsync(CancellationToken cancellationToken)
    {
        if (_disposed || IsFormOpen) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var selectedId = SelectedItem?.Id;
        _catalog.Invalidate();
        try
        {
            SetStatus(SettingsText.Get("McpReconnectStarted"));
            await _manager.ReloadAsync(operation.Token);
            await LoadAsync(selectedId, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            RefreshConnectionStatus();
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            SetStatus(SettingsText.Get("McpReconnectCancelled"));
        }
        catch (Exception)
        {
            SetStatus(SettingsText.Get("McpReconnectFailed"), true);
        }
    }

    private async Task LoadSafelyAsync()
    {
        var generation = _catalog.Generation + 1;
        try
        {
            await LoadAsync();
        }
        catch (Exception)
        {
            if (generation == _catalog.Generation) SetStatus(UiText.T("无法加载 MCP 服务列表，请检查数据目录并重试。"), true);
        }
    }

    private async Task RunMutationAsync(Func<long, CancellationToken, Task> mutation,
        CancellationToken cancellationToken, string failureKey)
    {
        if (_disposed) return;
        var revision = EditorRevision;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try { await mutation(revision, operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { ReportStatus(revision, "McpOperationCancelled", false); }
        catch (Exception) { ReportStatus(revision, failureKey); }
    }

    private bool OwnsEditor(long revision) => !_disposed && revision == EditorRevision;
    private void ReportStatus(long revision, string key, bool isError = true)
    {
        if (OwnsEditor(revision)) SetStatus(SettingsText.Get(key), isError);
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (_disposed) return;
        Status = message;
        HasStatusError = isError;
    }

    private void RefreshConnectionStatus()
    {
        var count = _tools.Descriptors.Count(tool => tool.Name.StartsWith("mcp.", StringComparison.Ordinal));
        SetStatus(_manager.Errors.Count == 0
            ? UiText.T("MCP 已重新加载，共发现 {0} 个工具。", count)
            : string.Join("；", _manager.Errors.Select(static pair => $"{pair.Key}: {pair.Value}")), _manager.Errors.Count > 0);
    }

    private void HandleStartupChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(AppStartupViewModel.IsLoading) || _startup?.IsLoading != false) return;
        if (_startup.HasWarning && _manager.Errors.Count == 0) SetStatus(_startup.Status);
        else RefreshConnectionStatus();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _catalog.Dispose();
        foreach (var command in new IAsyncRelayCommand[] { SaveCommand, ReloadCommand, ToggleEnabledCommand, DeleteCommand })
        {
            command.PropertyChanged -= HandleOperationChanged;
            ((IDisposable)command).Dispose();
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
        if (_startup is not null) _startup.PropertyChanged -= HandleStartupChanged;
    }

    private void HandleOperationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || args.PropertyName != nameof(IAsyncRelayCommand.IsRunning)) return;
        OnPropertyChanged(nameof(CanEdit));
        SaveCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        NewCommand.NotifyCanExecuteChanged();
        OpenCreateFormCommand.NotifyCanExecuteChanged();
        OpenEditFormCommand.NotifyCanExecuteChanged();
        ToggleEnabledCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void OpenEditForm(McpServerProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        SelectedItem = profile;
        SetStatus(string.Empty);
        IsFormOpen = true;
    }

    private void LoadSelection(McpServerProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        _editorId = profile.Id;
        _editingExisting = true;
        Name = profile.Name;
        Transport = profile.Transport;
        Command = profile.Command ?? string.Empty;
        ArgumentsJson = profile.ArgumentsJson;
        WorkingDirectory = profile.WorkingDirectory ?? string.Empty;
        EnvironmentJson = profile.EnvironmentJson;
        Url = profile.Url?.ToString() ?? string.Empty;
        HeadersJson = profile.HeadersJson;
        SelectedProxy = ProxyOptions.FirstOrDefault(item => item.Id == profile.ProxyId)
            ?? ProxyOptions.FirstOrDefault();
        IsEnabled = profile.IsEnabled;
    }

    private void ResetEditor()
    {
        SelectedItem = null;
        _editorId = Guid.NewGuid();
        _editingExisting = false;
        InvalidateEditor();
        Name = string.Empty;
        Transport = McpTransportKind.Stdio;
        Command = string.Empty;
        ArgumentsJson = "[]";
        WorkingDirectory = string.Empty;
        EnvironmentJson = "{}";
        Url = string.Empty;
        HeadersJson = "{}";
        SelectedProxy = ProxyOptions.FirstOrDefault();
        IsEnabled = true;
        SetStatus(string.Empty);
        CancelDeleteConfirmation();
    }

    private bool HasSelection() => CanEdit && SelectedItem is not null;

    private void SetSelectionReference(McpServerProfile? profile)
    {
        if (!SetProperty(ref _selectedItem, profile, nameof(SelectedItem))) return;
        DeleteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(CanDelete));
    }

    private void CancelDeleteConfirmation()
    {
        if (!_deleteConfirmationPending)
        {
            return;
        }

        _deleteConfirmationPending = false;
        OnPropertyChanged(nameof(DeleteButtonText));
    }
}

