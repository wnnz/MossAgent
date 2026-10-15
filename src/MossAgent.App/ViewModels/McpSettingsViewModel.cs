using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class McpSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IMcpConfigurationRepository _configurations;
    private readonly IConfigurationRepository _proxies;
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
    private bool _isFormOpen;

    public McpSettingsViewModel(
        IMcpConfigurationRepository configurations,
        IConfigurationRepository proxies,
        IMcpManager manager,
        IToolCatalog tools,
        AppStartupViewModel? startup = null)
    {
        _configurations = configurations;
        _proxies = proxies;
        _manager = manager;
        _tools = tools;
        _startup = startup;
        if (_startup is not null) _startup.PropertyChanged += HandleStartupChanged;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoItems));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadFromUiAsync);
        NewCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; });
        OpenEditFormCommand = new RelayCommand<McpServerProfile>(OpenEditForm);
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        ToggleEnabledCommand = new AsyncRelayCommand<McpServerProfile>(ToggleEnabledAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, HasSelection);
        _ = LoadSafelyAsync();
    }

    public ObservableCollection<McpServerProfile> Items { get; } = [];
    public bool HasNoItems => Items.Count == 0;
    public ObservableCollection<ProxySelection> ProxyOptions { get; } = [];
    public IReadOnlyList<McpTransportOption> TransportOptions { get; } = McpTransportOption.All;
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand<McpServerProfile> OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public IAsyncRelayCommand<McpServerProfile> ToggleEnabledCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// 是否弹出 MCP 服务配置表单弹窗。
    /// </summary>
    public bool IsFormOpen
    {
        get => _isFormOpen;
        set => SetProperty(ref _isFormOpen, value);
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
    public ProxySelection? SelectedProxy { get => _selectedProxy; set => SetProperty(ref _selectedProxy, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public McpServerProfile? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetProperty(ref _selectedItem, value))
            {
                return;
            }

            CancelDeleteConfirmation();
            LoadSelection(value);
            DeleteCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(EditorTitle));
            OnPropertyChanged(nameof(CanDelete));
        }
    }

    public string EditorTitle => SelectedItem is null ? "添加 MCP Server" : "编辑 MCP Server";
    public bool CanDelete => SelectedItem is not null;
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public string DeleteButtonText => _deleteConfirmationPending ? "确认删除" : "删除";
    public string Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }

    private async Task LoadAsync(Guid? selectedId = null)
    {
        var servers = await _configurations.GetAllAsync(CancellationToken.None);
        var proxies = await _proxies.GetProxiesAsync(CancellationToken.None);
        Items.ReplaceWith(servers);
        var proxyId = SelectedProxy?.Id;
        ProxyOptions.ReplaceWith(ProxySelection.FromProfiles(proxies, "直连"));

        SelectedProxy = ProxyOptions.FirstOrDefault(item => item.Id == proxyId) ?? ProxyOptions[0];
        if (selectedId is not null)
        {
            SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId);
        }
    }

    private async Task SaveAsync()
    {
        var argumentsJson = IsStdioTransport ? ArgumentsJson : "[]";
        var environmentJson = IsStdioTransport ? EnvironmentJson : "{}";
        var headersJson = IsHttpTransport ? HeadersJson : "{}";
        if (!McpSettingsValidator.TryValidate(
                Name, Transport, Command, Url, argumentsJson, environmentJson,
                headersJson, out var endpoint, out var error))
        {
            Status = error;
            return;
        }

        var profile = new McpServerProfile(
            SelectedItem?.Id ?? Guid.NewGuid(), Name.Trim(), Transport,
            IsStdioTransport ? EmptyToNull(Command) : null,
            argumentsJson, IsStdioTransport ? EmptyToNull(WorkingDirectory) : null, environmentJson,
            endpoint, headersJson, IsHttpTransport ? SelectedProxy?.Id : null, IsEnabled);
        try
        {
            var existing = await _configurations.GetAllAsync(CancellationToken.None);
            if (existing.Any(item => item.Id != profile.Id
                && string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Status = "已有同名 MCP 服务，请使用其他名称。";
                return;
            }

            await _configurations.SaveAsync(profile, CancellationToken.None);
        }
        catch (Exception)
        {
            Status = "无法保存 MCP 服务，请检查名称是否重复、数据目录是否可写。";
            return;
        }

        if (!await TryRefreshAsync(profile.Id, "已保存 MCP 服务，但无法刷新连接状态，请重试。"))
        {
            return;
        }

        var connectionFailed = _manager.Errors.TryGetValue(profile.Name, out var connectionError);
        Status = connectionFailed
            ? $"已保存 MCP server：{profile.Name}，但连接失败：{connectionError}"
            : $"已保存 MCP server：{profile.Name}";
        IsFormOpen = connectionFailed;
    }

    private async Task ToggleEnabledAsync(McpServerProfile? selected)
    {
        if (selected is null)
        {
            return;
        }

        McpServerProfile updated;
        try
        {
            var profiles = await _configurations.GetAllAsync(CancellationToken.None);
            var current = profiles.FirstOrDefault(item => item.Id == selected.Id);
            if (current is null)
            {
                Status = "MCP 服务已不存在，请刷新列表。";
                return;
            }

            updated = current with { IsEnabled = !current.IsEnabled };
            await _configurations.SaveAsync(updated, CancellationToken.None);
        }
        catch (Exception)
        {
            Status = "无法切换 MCP 服务状态，请检查数据目录并重试。";
            return;
        }

        if (!await TryRefreshAsync(updated.Id, "状态已保存，但无法刷新 MCP 连接，请重试。"))
        {
            return;
        }

        Status = !updated.IsEnabled
            ? "MCP server 已禁用。"
            : _manager.Errors.TryGetValue(updated.Name, out var error)
                ? $"MCP server 已启用，但连接失败：{error}"
                : "MCP server 已启用并连接。";
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } selected)
        {
            return;
        }

        if (!_deleteConfirmationPending)
        {
            _deleteConfirmationPending = true;
            OnPropertyChanged(nameof(DeleteButtonText));
            Status = $"再次点击“确认删除”以删除 {selected.Name}。";
            return;
        }

        try
        {
            await _configurations.DeleteAsync(selected.Id, CancellationToken.None);
        }
        catch (Exception)
        {
            Status = "无法删除 MCP 服务，请检查数据目录并重试。";
            return;
        }

        ResetEditor();
        if (!await TryRefreshAsync(null, "服务已删除，但无法刷新 MCP 列表，请重试。"))
        {
            return;
        }

        Status = $"已删除 MCP server：{selected.Name}";
    }

    private async Task<bool> TryRefreshAsync(Guid? selectedId, string failureStatus)
    {
        try
        {
            await ReloadAsync();
            await LoadAsync(selectedId);
            return true;
        }
        catch (Exception)
        {
            Status = failureStatus;
            return false;
        }
    }

    private async Task ReloadFromUiAsync()
    {
        try
        {
            await ReloadAsync();
        }
        catch (Exception)
        {
            Status = "无法重新连接 MCP 服务，请检查数据目录并重试。";
        }
    }

    private async Task LoadSafelyAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception)
        {
            Status = "无法加载 MCP 服务列表，请检查数据目录并重试。";
        }
    }

    private async Task ReloadAsync()
    {
        await _manager.ReloadAsync(CancellationToken.None);
        RefreshConnectionStatus();
    }

    private void RefreshConnectionStatus()
    {
        var count = _tools.Descriptors.Count(tool => tool.Name.StartsWith("mcp.", StringComparison.Ordinal));
        Status = _manager.Errors.Count == 0
            ? $"MCP 已重新加载，共发现 {count} 个工具。"
            : string.Join("；", _manager.Errors.Select(static pair => $"{pair.Key}: {pair.Value}"));
    }

    private void HandleStartupChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(AppStartupViewModel.IsLoading) || _startup?.IsLoading != false) return;
        if (_startup.HasWarning && _manager.Errors.Count == 0) Status = _startup.Status;
        else RefreshConnectionStatus();
    }

    public void Dispose()
    {
        if (_startup is not null) _startup.PropertyChanged -= HandleStartupChanged;
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void OpenEditForm(McpServerProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        SelectedItem = profile;
        Status = string.Empty;
        IsFormOpen = true;
    }

    private void LoadSelection(McpServerProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

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
        Status = string.Empty;
        CancelDeleteConfirmation();
    }

    private bool HasSelection() => SelectedItem is not null;

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
