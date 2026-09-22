using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ProviderSettingsViewModel : ObservableObject
{
    private readonly IConfigurationRepository _repository;
    private AiProvider? _selectedItem;
    private string _name = string.Empty;
    private string _baseUrl = "https://api.openai.com/v1/";
    private string _apiKey = string.Empty;
    private ProviderProtocol _protocol = ProviderProtocol.OpenAiResponses;
    private bool _isDefault;
    private bool _isEnabled = true;
    private ProxySelection? _selectedProxy;
    private string _headersJson = "{}";
    private string _status = string.Empty;
    private Guid? _deleteConfirmationId;
    private bool _isFormOpen;

    public ProviderSettingsViewModel(IConfigurationRepository repository)
    {
        _repository = repository;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        NewCommand = new RelayCommand(ResetEditor);
        OpenCreateFormCommand = new RelayCommand(() => { ResetEditor(); IsFormOpen = true; });
        OpenEditFormCommand = new RelayCommand(() => { if (SelectedItem is not null) IsFormOpen = true; });
        CloseFormCommand = new RelayCommand(() => IsFormOpen = false);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, HasSelection);
        _ = LoadAsync();
    }

    public ObservableCollection<AiProvider> Items { get; } = [];
    public ObservableCollection<ProxySelection> Proxies { get; } = [];
    public IReadOnlyList<ProviderProtocol> Protocols { get; } = Enum.GetValues<ProviderProtocol>();
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCreateFormCommand { get; }
    public IRelayCommand OpenEditFormCommand { get; }
    public IRelayCommand CloseFormCommand { get; }
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

            CancelDeleteConfirmation();
            DeleteCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(EditorTitle));
        }
    }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    public string ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }
    public ProviderProtocol Protocol { get => _protocol; set => SetProperty(ref _protocol, value); }
    public bool IsDefault { get => _isDefault; set => SetProperty(ref _isDefault, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public ProxySelection? SelectedProxy { get => _selectedProxy; set => SetProperty(ref _selectedProxy, value); }
    public string HeadersJson { get => _headersJson; set => SetProperty(ref _headersJson, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string EditorTitle => SelectedItem is null ? "添加 AI 供应商" : "编辑 AI 供应商";
    public string DeleteButtonText => _deleteConfirmationId == SelectedItem?.Id
        ? "确认删除"
        : "删除";

    private async Task LoadAsync()
    {
        var selectedId = SelectedItem?.Id;
        var providers = await _repository.GetProvidersAsync(CancellationToken.None);
        var proxies = await _repository.GetProxiesAsync(CancellationToken.None);
        Items.Clear();
        foreach (var provider in providers)
        {
            Items.Add(provider);
        }

        Proxies.Clear();
        Proxies.Add(new ProxySelection(null, "直连"));
        foreach (var proxy in proxies.Where(static proxy => proxy.IsEnabled))
        {
            Proxies.Add(new ProxySelection(proxy.Id, proxy.Name));
        }

        SelectedItem = Items.FirstOrDefault(provider => provider.Id == selectedId);
        SelectedProxy ??= Proxies[0];
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
        {
            Status = "请填写有效的名称和 Base URL。";
            return;
        }

        Dictionary<string, string> headers;
        try
        {
            headers = JsonSerializer.Deserialize<Dictionary<string, string>>(HeadersJson) ?? [];
        }
        catch (JsonException)
        {
            Status = "额外请求头必须是字符串键值 JSON 对象。";
            return;
        }

        var provider = new AiProvider(
            SelectedItem?.Id ?? Guid.NewGuid(), Name.Trim(), Protocol, uri,
            string.IsNullOrWhiteSpace(ApiKey) && SelectedItem is not null
                ? SelectedItem.ApiKey
                : ApiKey.Trim(),
            SelectedProxy?.Id,
            IsDefault, IsEnabled, headers);
        await _repository.SaveProviderAsync(provider, CancellationToken.None);
        Status = SelectedItem is null ? "供应商已添加。" : "供应商设置已更新。";
        IsFormOpen = false;
        ResetEditor();
        await LoadAsync();
    }

    private async Task DeleteAsync()
    {
        var provider = SelectedItem;
        if (provider is null)
        {
            return;
        }

        if (_deleteConfirmationId != provider.Id)
        {
            _deleteConfirmationId = provider.Id;
            OnPropertyChanged(nameof(DeleteButtonText));
            Status = "再次点击确认删除；该供应商下的模型配置也会删除。";
            return;
        }

        await _repository.DeleteProviderAsync(provider.Id, CancellationToken.None);
        Status = $"已删除供应商：{provider.Name}";
        ResetEditor();
        await LoadAsync();
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
        SelectedProxy = Proxies.FirstOrDefault(proxy => proxy.Id == provider.ProxyId)
            ?? Proxies.FirstOrDefault();
        Status = "API Key 留空将保留当前值。";
    }

    private void ResetEditor()
    {
        SelectedItem = null;
        Name = string.Empty;
        BaseUrl = "https://api.openai.com/v1/";
        ApiKey = string.Empty;
        Protocol = ProviderProtocol.OpenAiResponses;
        IsDefault = false;
        IsEnabled = true;
        HeadersJson = "{}";
        SelectedProxy = Proxies.FirstOrDefault();
    }

    private bool HasSelection() => SelectedItem is not null;

    private void CancelDeleteConfirmation()
    {
        if (_deleteConfirmationId is null)
        {
            return;
        }

        _deleteConfirmationId = null;
        OnPropertyChanged(nameof(DeleteButtonText));
    }
}
