using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Networking;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ProxySettingsViewModel : ObservableObject
{
    private readonly IConfigurationRepository _repository;
    private readonly IProxyConnectionTester _tester;
    private ProxyProfile? _selectedItem;
    private string _name = string.Empty;
    private string _host = "127.0.0.1";
    private int _port = 7890;
    private ProxyProtocol _protocol = ProxyProtocol.Http;
    private bool _isDefault;
    private bool _isEnabled = true;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _status = string.Empty;
    private bool _isFormOpen;

    public ProxySettingsViewModel(
        IConfigurationRepository repository,
        IProxyConnectionTester tester)
    {
        _repository = repository;
        _tester = tester;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        TestCommand = new AsyncRelayCommand(TestAsync, HasSelection);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, HasSelection);
        NewCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; });
        OpenEditFormCommand = new RelayCommand(() => { if (SelectedItem is not null) IsFormOpen = true; });
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        _ = LoadAsync();
    }

    public ObservableCollection<ProxyProfile> Items { get; } = [];
    public IReadOnlyList<ProxyProtocol> Protocols { get; } = Enum.GetValues<ProxyProtocol>();
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand TestCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }

    /// <summary>
    /// 是否弹出代理配置表单弹窗。
    /// </summary>
    public bool IsFormOpen
    {
        get => _isFormOpen;
        set => SetProperty(ref _isFormOpen, value);
    }

    public ProxyProfile? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value is not null)
            {
                LoadEditor(value);
            }

            TestCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
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

    private async Task LoadAsync()
    {
        var selectedId = SelectedItem?.Id;
        Items.ReplaceWith(await _repository.GetProxiesAsync(CancellationToken.None));
        SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Host)
            || Port is < 1 or > 65535)
        {
            Status = "请填写有效的名称、地址和端口。";
            return;
        }

        var proxy = new ProxyProfile(
            SelectedItem?.Id ?? Guid.NewGuid(), Name.Trim(), Protocol, Host.Trim(), Port,
            EmptyToNull(Username), EmptyToNull(Password), IsDefault, IsEnabled);
        await _repository.SaveProxyAsync(proxy, CancellationToken.None);
        Status = SelectedItem is null ? "代理已添加。" : "代理设置已更新。";
        IsFormOpen = false;
        ResetEditor();
        await LoadAsync();
    }

    private async Task TestAsync()
    {
        Status = "正在通过所选代理访问连通性探测地址…";
        var result = await _tester.TestAsync(SelectedItem!, CancellationToken.None);
        Status = $"{result.Message} 耗时 {result.Elapsed.TotalMilliseconds:F0} ms。";
    }

    private async Task DeleteAsync()
    {
        var proxy = SelectedItem!;
        await _repository.DeleteProxyAsync(proxy.Id, CancellationToken.None);
        Status = $"已删除代理：{proxy.Name}";
        ResetEditor();
        await LoadAsync();
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
        SelectedItem = null;
        Name = string.Empty;
        Host = "127.0.0.1";
        Port = 7890;
        Username = string.Empty;
        Password = string.Empty;
        IsDefault = false;
        IsEnabled = true;
    }

    private bool HasSelection() => SelectedItem is not null;

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
