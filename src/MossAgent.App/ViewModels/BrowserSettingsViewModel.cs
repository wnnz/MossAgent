using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class BrowserSettingsViewModel : ObservableObject
{
    private readonly IBrowserConfigurationRepository _browserConfigurations;
    private readonly IConfigurationRepository _configurations;
    private DefaultBrowserKind _defaultBrowser = DefaultBrowserKind.Embedded;
    private BrowserProfilePreference _profilePreference = BrowserProfilePreference.Managed;
    private string _profileDirectory = string.Empty;
    private string _cdpEndpoint = "http://127.0.0.1:9222";
    private ProxySelection? _selectedProxy;
    private string _status = string.Empty;

    public BrowserSettingsViewModel(
        IBrowserConfigurationRepository browserConfigurations,
        IConfigurationRepository configurations)
    {
        _browserConfigurations = browserConfigurations;
        _configurations = configurations;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public IReadOnlyList<DefaultBrowserKind> BrowserKinds { get; } = Enum.GetValues<DefaultBrowserKind>();
    public IReadOnlyList<BrowserProfilePreference> ProfilePreferences { get; } = Enum.GetValues<BrowserProfilePreference>();
    public ObservableCollection<ProxySelection> Proxies { get; } = [];
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public DefaultBrowserKind DefaultBrowser { get => _defaultBrowser; set => SetProperty(ref _defaultBrowser, value); }
    public BrowserProfilePreference ProfilePreference { get => _profilePreference; set => SetProperty(ref _profilePreference, value); }
    public string ProfileDirectory { get => _profileDirectory; set => SetProperty(ref _profileDirectory, value); }
    public string CdpEndpoint { get => _cdpEndpoint; set => SetProperty(ref _cdpEndpoint, value); }
    public ProxySelection? SelectedProxy { get => _selectedProxy; set => SetProperty(ref _selectedProxy, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    private async Task LoadAsync()
    {
        var configuration = await _browserConfigurations.GetAsync(CancellationToken.None);
        var proxies = await _configurations.GetProxiesAsync(CancellationToken.None);
        DefaultBrowser = configuration.DefaultBrowser;
        ProfilePreference = configuration.ProfilePreference;
        ProfileDirectory = configuration.ProfileDirectory ?? string.Empty;
        CdpEndpoint = configuration.CdpEndpoint?.ToString() ?? "http://127.0.0.1:9222";
        Proxies.Clear();
        Proxies.Add(new ProxySelection(null, "直连"));
        foreach (var proxy in proxies.Where(static proxy => proxy.IsEnabled))
        {
            Proxies.Add(new ProxySelection(proxy.Id, proxy.Name));
        }

        SelectedProxy = Proxies.FirstOrDefault(item => item.Id == configuration.ProxyId) ?? Proxies[0];
    }

    private async Task SaveAsync()
    {
        if (DefaultBrowser == DefaultBrowserKind.Embedded
            && ProfilePreference == BrowserProfilePreference.ExistingCdp)
        {
            Status = "内置浏览器不能连接已有 CDP；请选择托管或日常资料模式。";
            return;
        }

        Uri? cdp = null;
        if (ProfilePreference == BrowserProfilePreference.ExistingCdp
            && !Uri.TryCreate(CdpEndpoint, UriKind.Absolute, out cdp))
        {
            Status = "CDP 地址无效。";
            return;
        }

        if (ProfilePreference == BrowserProfilePreference.DailyProfile
            && string.IsNullOrWhiteSpace(ProfileDirectory))
        {
            Status = "日常资料模式必须填写浏览器资料目录。";
            return;
        }

        await _browserConfigurations.SaveAsync(
            new BrowserConfiguration(
                DefaultBrowser, ProfilePreference,
                EmptyToNull(ProfileDirectory), cdp, SelectedProxy?.Id),
            CancellationToken.None);
        Status = "浏览器设置已保存，新任务将使用这些设置。";
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
