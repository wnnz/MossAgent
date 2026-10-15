using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Resources;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class BrowserSettingsViewModel : SettingsEditorViewModel, IDisposable
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.BrowserSettingsStrings", typeof(BrowserSettingsViewModel).Assembly);
    private readonly IBrowserConfigurationRepository _browserConfigurations;
    private readonly IConfigurationRepository _configurations;
    private readonly SettingsMutationRunner _mutations = new();
    private readonly SettingsOperationLifetime _operations;
    private readonly HashSet<Guid> _enabledProxyIds = [];
    private long _cleanRevision;
    private int _loadVersion;
    private bool _catalogAvailable;
    private bool _publishingProxies;
    private bool _proxyReloadRequested;
    private bool _drainingProxyReloads;
    private bool _isLoading;
    private bool _hasStatusError;
    private bool _disposed;
    private DefaultBrowserKind _defaultBrowser = DefaultBrowserKind.Embedded;
    private BrowserProfilePreference _profilePreference = BrowserProfilePreference.Managed;
    private string _profileDirectory = string.Empty;
    private string _cdpEndpoint = "http://127.0.0.1:9222";
    private ProxySelection? _selectedProxy;
    private string _status = string.Empty;

    public BrowserSettingsViewModel(
        IBrowserConfigurationRepository browserConfigurations,
        IConfigurationRepository configurations, TimeSpan? operationTimeout = null)
        : base(nameof(DefaultBrowser), nameof(ProfilePreference), nameof(ProfileDirectory), nameof(CdpEndpoint))
    {
        _browserConfigurations = browserConfigurations;
        _configurations = configurations;
        _operations = new SettingsOperationLifetime(operationTimeout);
        _mutations.PropertyChanged += HandleMutationChanged;
        SaveCommand = new GuardedAsyncRelayCommand(SaveAsync, CanSave);
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync,
            () => !_disposed && !IsLoading && !IsBusy && !IsReloadingProxies);
        Initialization = ReadAsync();
    }

    public IReadOnlyList<BrowserSettingOption<DefaultBrowserKind>> BrowserKinds { get; } =
    [
        new(DefaultBrowserKind.Embedded, GetLabel("EmbeddedBrowser")),
        new(DefaultBrowserKind.Chrome, GetLabel("ChromeBrowser")),
        new(DefaultBrowserKind.Edge, GetLabel("EdgeBrowser"))
    ];
    public IReadOnlyList<BrowserSettingOption<BrowserProfilePreference>> ProfilePreferences { get; } =
    [
        new(BrowserProfilePreference.Managed, GetLabel("ManagedProfile")),
        new(BrowserProfilePreference.DailyProfile, GetLabel("DailyProfile")),
        new(BrowserProfilePreference.ExistingCdp, GetLabel("ExistingCdpProfile"))
    ];
    public ObservableCollection<ProxySelection> Proxies { get; } = [];
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public Task Initialization { get; }
    public bool IsBusy => _mutations.IsRunning;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }
    public bool IsProxyUnavailable => _catalogAvailable && SelectedProxy?.Id is { } id && !_enabledProxyIds.Contains(id);
    private bool IsReloadingProxies => _proxyReloadRequested || _drainingProxyReloads;
    public bool HasEditorNotice => IsLoading || IsReloadingProxies || IsProxyUnavailable || !_catalogAvailable;
    public string EditorNotice => GetLabel(IsLoading || IsReloadingProxies
        ? "Loading" : IsProxyUnavailable ? "ProxyUnavailable" : "ReadRequired");
    public string SaveLabel => GetLabel(IsBusy ? "Saving" : "SaveLabel");
    public DefaultBrowserKind DefaultBrowser
    {
        get => _defaultBrowser;
        set
        {
            if (SetProperty(ref _defaultBrowser, value)) OnPropertyChanged(nameof(SelectedBrowserKind));
        }
    }
    public BrowserSettingOption<DefaultBrowserKind>? SelectedBrowserKind
    {
        get => BrowserKinds.FirstOrDefault(option => option.Value == DefaultBrowser);
        set { if (value is not null) DefaultBrowser = value.Value; }
    }
    public BrowserProfilePreference ProfilePreference
    {
        get => _profilePreference;
        set
        {
            if (!SetProperty(ref _profilePreference, value)) return;
            OnPropertyChanged(nameof(SelectedProfilePreference));
            OnPropertyChanged(nameof(IsDailyProfile));
            OnPropertyChanged(nameof(IsExistingCdp));
        }
    }
    public BrowserSettingOption<BrowserProfilePreference>? SelectedProfilePreference
    {
        get => ProfilePreferences.FirstOrDefault(option => option.Value == ProfilePreference);
        set { if (value is not null) ProfilePreference = value.Value; }
    }
    public bool IsDailyProfile => ProfilePreference == BrowserProfilePreference.DailyProfile;
    public bool IsExistingCdp => ProfilePreference == BrowserProfilePreference.ExistingCdp;
    public string ProfileDirectory { get => _profileDirectory; set => SetProperty(ref _profileDirectory, value); }
    public string CdpEndpoint { get => _cdpEndpoint; set => SetProperty(ref _cdpEndpoint, value); }
    public ProxySelection? SelectedProxy
    {
        get => _selectedProxy;
        set
        {
            if (_publishingProxies || _disposed) return;
            if (_selectedProxy?.Id != value?.Id) InvalidateEditor();
            if (SetProperty(ref _selectedProxy, value)) NotifyCommands();
        }
    }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public Task ReloadProxiesAsync()
    {
        if (_disposed) return Task.CompletedTask;
        _proxyReloadRequested = true;
        NotifyCommands();
        return IsLoading || IsBusy || _drainingProxyReloads ? Task.CompletedTask : DrainProxyReloadsAsync();
    }

    private async Task DrainProxyReloadsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_proxyReloadRequested || IsLoading || _drainingProxyReloads) return;
        _drainingProxyReloads = true;
        NotifyCommands();
        try
        {
            while (!_disposed && _proxyReloadRequested && !cancellationToken.IsCancellationRequested)
            {
                _proxyReloadRequested = false;
                await ReadAsync(cancellationToken, refreshDraft: false);
            }
        }
        finally
        {
            _drainingProxyReloads = false;
            if (!_disposed) NotifyCommands();
        }
    }

    private async Task<bool> ReadAsync(CancellationToken cancellationToken = default, bool refreshDraft = true)
    {
        if (_disposed) return false;
        var version = ++_loadVersion;
        var revision = EditorRevision;
        var canReplaceDraft = refreshDraft && revision == _cleanRevision;
        using var operation = _operations.CreateOperation(cancellationToken);
        IsLoading = true;
        _catalogAvailable = false;
        NotifyCommands();
        try
        {
            var configuration = await _browserConfigurations.GetAsync(operation.Token).WaitAsync(operation.Token);
            var proxies = await _configurations.GetProxiesAsync(operation.Token).WaitAsync(operation.Token);
            if (_disposed || operation.IsCancellationRequested || version != _loadVersion) return false;
            var proxyId = SelectedProxy?.Id;
            if (canReplaceDraft && revision == EditorRevision)
            {
                DefaultBrowser = configuration.DefaultBrowser;
                ProfilePreference = configuration.ProfilePreference;
                ProfileDirectory = configuration.ProfileDirectory ?? string.Empty;
                CdpEndpoint = configuration.CdpEndpoint?.ToString() ?? "http://127.0.0.1:9222";
                proxyId = configuration.ProxyId;
                _cleanRevision = EditorRevision;
            }
            PublishProxies(proxies, proxyId);
            _catalogAvailable = true;
            return true;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            ReportCancellation(cancellationToken, revision);
            return false;
        }
        catch (Exception)
        {
            if (!_disposed && revision == EditorRevision) SetStatus("LoadFailed", true);
            return false;
        }
        finally
        {
            if (!_disposed && version == _loadVersion)
            {
                IsLoading = false;
                NotifyCommands();
                if (!IsBusy) await DrainProxyReloadsAsync();
            }
        }
    }

    private void PublishProxies(IReadOnlyList<ProxyProfile> proxies, Guid? selectedId)
    {
        _enabledProxyIds.Clear();
        var options = new List<ProxySelection>();
        foreach (var option in ProxySelection.FromProfiles(proxies, GetLabel("DirectConnection")))
        {
            options.Add(option);
            if (option.Id is { } proxyId) _enabledProxyIds.Add(proxyId);
        }
        if (selectedId is { } id && !_enabledProxyIds.Contains(id))
            options.Add(new ProxySelection(id, GetLabel("UnavailableProxyOption")));
        _publishingProxies = true;
        try { if (!Proxies.SequenceEqual(options)) Proxies.ReplaceWith(options); }
        finally { _publishingProxies = false; }
        _selectedProxy = Proxies.Single(proxy => proxy.Id == selectedId);
        OnPropertyChanged(nameof(SelectedProxy));
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ClearStatus();
        await ReadAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!CanSave() || !TryCaptureConfiguration(out var configuration)) return;
        var revision = EditorRevision;
        using var operation = _operations.CreateOperation(cancellationToken);
        ClearStatus();
        await _mutations.TryExecuteAsync(async token =>
        {
            if (!await ReadAsync(token, refreshDraft: false)) return;
            await DrainProxyReloadsAsync(token);
            if (_disposed || token.IsCancellationRequested || !_catalogAvailable) return;
            if (configuration.ProxyId is { } id && !_enabledProxyIds.Contains(id))
            {
                if (revision == EditorRevision) SetStatus("ProxyUnavailable", true);
                return;
            }
            await _browserConfigurations.SaveAsync(configuration, token);
            if (_disposed || token.IsCancellationRequested) return;
            if (revision == EditorRevision) _cleanRevision = revision;
            SetStatus(revision == EditorRevision ? "Saved" : "PreviousDraftSaved", false);
        }, operation.Token, () =>
        {
            if (!_disposed && revision == EditorRevision) SetStatus("SaveFailed", true);
        });
        if (operation.IsCancellationRequested) ReportCancellation(cancellationToken, revision);
        await DrainProxyReloadsAsync();
    }

    private bool TryCaptureConfiguration(out BrowserConfiguration configuration)
    {
        configuration = new(DefaultBrowser, ProfilePreference, EmptyToNull(ProfileDirectory), null, SelectedProxy?.Id);
        if (!Enum.IsDefined(DefaultBrowser) || !Enum.IsDefined(ProfilePreference))
        {
            SetStatus("InvalidSelection", true);
            return false;
        }
        if (DefaultBrowser == DefaultBrowserKind.Embedded
            && ProfilePreference == BrowserProfilePreference.ExistingCdp)
        {
            SetStatus("EmbeddedCdpUnsupported", true);
            return false;
        }

        Uri? cdp = null;
        if (ProfilePreference == BrowserProfilePreference.ExistingCdp
            && !TryParseCdpEndpoint(CdpEndpoint, out cdp))
        {
            SetStatus("InvalidCdpEndpoint", true);
            return false;
        }

        if (ProfilePreference == BrowserProfilePreference.DailyProfile
            && string.IsNullOrWhiteSpace(ProfileDirectory))
        {
            SetStatus("ProfileDirectoryRequired", true);
            return false;
        }
        configuration = configuration with { CdpEndpoint = cdp };
        return true;
    }

    private bool CanSave() => !_disposed && _catalogAvailable && !IsLoading && !IsBusy && !IsReloadingProxies
        && SelectedProxy is not null && !IsProxyUnavailable;

    private void ReportCancellation(CancellationToken owner, long revision)
    {
        if (!_disposed && revision == EditorRevision)
            SetStatus(owner.IsCancellationRequested ? "Cancelled" : "TimedOut", true);
    }

    private void SetStatus(string key, bool error) { HasStatusError = error; Status = GetLabel(key); }
    private void ClearStatus() { HasStatusError = false; Status = string.Empty; }
    private void HandleMutationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(SaveLabel));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsProxyUnavailable));
        OnPropertyChanged(nameof(HasEditorNotice));
        OnPropertyChanged(nameof(EditorNotice));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _proxyReloadRequested = false;
        _loadVersion++;
        _operations.Dispose();
        _mutations.PropertyChanged -= HandleMutationChanged;
        ((IDisposable)SaveCommand).Dispose();
        ((IDisposable)RefreshCommand).Dispose();
    }

    private static bool TryParseCdpEndpoint(string value, out Uri? endpoint)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out endpoint)
            || !BrowserCdpEndpointPolicy.IsAllowed(endpoint))
        {
            endpoint = null;
            return false;
        }

        return true;
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetLabel(string key) =>
        Labels.GetString(key) ?? throw new InvalidOperationException($"Missing browser settings resource: {key}");
}
