using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class McpSettingsViewModel : ObservableObject
{
    private readonly IMcpConfigurationRepository _configurations;
    private readonly IConfigurationRepository _proxies;
    private readonly IMcpManager _manager;
    private readonly IToolCatalog _tools;
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
        IToolCatalog tools)
    {
        _configurations = configurations;
        _proxies = proxies;
        _manager = manager;
        _tools = tools;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        NewCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; });
        OpenEditFormCommand = new RelayCommand(() => { if (SelectedItem is not null) IsFormOpen = true; });
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        ToggleEnabledCommand = new AsyncRelayCommand(ToggleEnabledAsync, HasSelection);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, HasSelection);
        _ = LoadAsync();
    }

    public ObservableCollection<McpServerProfile> Items { get; } = [];
    public ObservableCollection<ProxySelection> ProxyOptions { get; } = [];
    public IReadOnlyList<McpTransportKind> Transports { get; } = Enum.GetValues<McpTransportKind>();
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
    public IAsyncRelayCommand ToggleEnabledCommand { get; }
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
    public McpTransportKind Transport { get => _transport; set => SetProperty(ref _transport, value); }
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
            ToggleEnabledCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }
    }

    public string EditorTitle => SelectedItem is null ? "添加 MCP Server" : "编辑 MCP Server";
    public string DeleteButtonText => _deleteConfirmationPending ? "确认删除" : "删除";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    private async Task LoadAsync(Guid? selectedId = null)
    {
        var servers = await _configurations.GetAllAsync(CancellationToken.None);
        var proxies = await _proxies.GetProxiesAsync(CancellationToken.None);
        Items.ReplaceWith(servers);
        var proxyId = SelectedProxy?.Id;
        ProxyOptions.Clear();
        ProxyOptions.Add(new ProxySelection(null, "直连"));
        foreach (var proxy in proxies.Where(static proxy => proxy.IsEnabled))
        {
            ProxyOptions.Add(new ProxySelection(proxy.Id, proxy.Name));
        }

        SelectedProxy = ProxyOptions.FirstOrDefault(item => item.Id == proxyId) ?? ProxyOptions[0];
        if (selectedId is not null)
        {
            SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId);
        }
    }

    private async Task SaveAsync()
    {
        if (!McpSettingsValidator.TryValidate(
                Name, Transport, Command, Url, ArgumentsJson, EnvironmentJson,
                HeadersJson, out var endpoint, out var error))
        {
            Status = error;
            return;
        }

        var profile = new McpServerProfile(
            SelectedItem?.Id ?? Guid.NewGuid(), Name.Trim(), Transport, EmptyToNull(Command),
            ArgumentsJson, EmptyToNull(WorkingDirectory), EnvironmentJson,
            endpoint, HeadersJson, SelectedProxy?.Id, IsEnabled);
        await _configurations.SaveAsync(profile, CancellationToken.None);
        await ReloadAsync();
        await LoadAsync(profile.Id);
        IsFormOpen = false;
        Status = $"已保存 MCP server：{profile.Name}";
    }

    private async Task ToggleEnabledAsync()
    {
        if (SelectedItem is not { } selected)
        {
            return;
        }

        var updated = selected with { IsEnabled = !selected.IsEnabled };
        await _configurations.SaveAsync(updated, CancellationToken.None);
        await ReloadAsync();
        await LoadAsync(updated.Id);
        Status = updated.IsEnabled ? "MCP server 已启用并连接。" : "MCP server 已禁用。";
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

        await _configurations.DeleteAsync(selected.Id, CancellationToken.None);
        await ReloadAsync();
        ResetEditor();
        await LoadAsync();
        Status = $"已删除 MCP server：{selected.Name}";
    }

    private async Task ReloadAsync()
    {
        await _manager.ReloadAsync(CancellationToken.None);
        var count = _tools.Descriptors.Count(tool => tool.Name.StartsWith("mcp.", StringComparison.Ordinal));
        Status = _manager.Errors.Count == 0
            ? $"MCP 已重新加载，共发现 {count} 个工具。"
            : string.Join("；", _manager.Errors.Select(static pair => $"{pair.Key}: {pair.Value}"));
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
