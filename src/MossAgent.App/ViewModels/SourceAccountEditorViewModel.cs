using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using MossAgent.Application.SourceControl;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class SourceAccountEditorViewModel : SettingsEditorViewModel
{
    private Guid _id;
    private long _storedRevision;
    private string _name = string.Empty;
    private string _endpoint = "https://api.github.com/";
    private string _accessToken = string.Empty;
    private bool _isEnabled = true;
    private bool _isOpen;
    private bool _proxyCatalogAvailable;
    private readonly HashSet<Guid> _enabledProxyIds = [];
    private ProxySelection? _selectedProxy;

    public SourceAccountEditorViewModel() : base(nameof(Name), nameof(Endpoint), nameof(AccessToken), nameof(IsEnabled)) { }
    public long RevisionStamp => EditorRevision;
    public long StoredRevision => _storedRevision;
    public Guid Id => _id;
    public ObservableCollection<ProxySelection> Proxies { get; } = [];
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Endpoint { get => _endpoint; set => SetProperty(ref _endpoint, value); }
    [JsonIgnore]
    public string AccessToken { get => _accessToken; set => SetProperty(ref _accessToken, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public ProxySelection? SelectedProxy
    {
        get => _selectedProxy;
        set
        {
            if (_selectedProxy?.Id != value?.Id) InvalidateEditor();
            SetProperty(ref _selectedProxy, value);
        }
    }

    internal void Open(SourceAccount? account)
    {
        InvalidateEditor();
        _id = account?.Id ?? Guid.NewGuid();
        _storedRevision = account?.Revision ?? 0;
        Name = account?.Name ?? string.Empty;
        Endpoint = account?.ApiEndpoint.AbsoluteUri ?? "https://api.github.com/";
        AccessToken = account?.AccessToken ?? string.Empty;
        IsEnabled = account?.IsEnabled ?? true;
        SelectProxy(account?.ProxyId);
        IsOpen = true;
    }

    internal void Close()
    {
        InvalidateEditor();
        IsOpen = false;
        AccessToken = string.Empty;
    }

    internal void PublishProxies(IReadOnlyList<ProxyProfile> profiles)
    {
        var selectedId = SelectedProxy?.Id;
        _enabledProxyIds.Clear();
        foreach (var profile in profiles.Where(profile => profile.IsEnabled)) _enabledProxyIds.Add(profile.Id);
        Proxies.ReplaceWith(ProxySelection.FromProfiles(profiles, SourceAccountText.Get("Direct")));
        _proxyCatalogAvailable = true;
        SelectProxy(selectedId);
    }

    internal void InvalidateProxies() => _proxyCatalogAvailable = false;

    internal SourceAccount Capture()
    {
        if (SelectedProxy is null || (IsEnabled && (!_proxyCatalogAvailable
            || (SelectedProxy.Id is { } id && !_enabledProxyIds.Contains(id)))))
            throw new SourceConnectionException(SourceConnectionFailure.ProxyUnavailable);
        return new SourceAccount(_id, Name, GitHubApiEndpoint.Parse(Endpoint), AccessToken,
            SelectedProxy.Id, IsEnabled, _storedRevision);
    }

    private void SelectProxy(Guid? id)
    {
        var selection = Proxies.FirstOrDefault(proxy => proxy.Id == id);
        if (selection is null && id is not null)
        {
            selection = new ProxySelection(id, SourceAccountText.Get("UnavailableProxy"));
            Proxies.Add(selection);
        }
        _selectedProxy = selection;
        OnPropertyChanged(nameof(SelectedProxy));
    }
}
