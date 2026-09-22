using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.Tools.Abstractions.Browser;
using NativeWebView.Core;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.ViewModels;

public sealed class EmbeddedBrowserPaneViewModel : ObservableObject
{
    private WebViewControl? _view;
    private IBrowserSession? _session;
    private Control? _content;
    private BrowserTabInfo? _selectedTab;
    private bool _updatingTabs;
    private string _address = "https://example.com";
    private string _status = "浏览器将在代理首次使用浏览器工具时启动。";

    public EmbeddedBrowserPaneViewModel()
    {
        NavigateCommand = new AsyncRelayCommand(NavigateAsync, HasView);
        BackCommand = new AsyncRelayCommand(GoBackAsync, CanGoBack);
        ForwardCommand = new AsyncRelayCommand(GoForwardAsync, CanGoForward);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, HasView);
        DevToolsCommand = new AsyncRelayCommand(OpenDevToolsAsync, HasView);
        NewTabCommand = new AsyncRelayCommand(NewTabAsync, HasSession);
        CloseTabCommand = new AsyncRelayCommand(CloseTabAsync, CanCloseTab);
    }

    public IAsyncRelayCommand NavigateCommand { get; }
    public IAsyncRelayCommand BackCommand { get; }
    public IAsyncRelayCommand ForwardCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IAsyncRelayCommand DevToolsCommand { get; }
    public IAsyncRelayCommand NewTabCommand { get; }
    public IAsyncRelayCommand CloseTabCommand { get; }
    public ObservableCollection<BrowserTabInfo> Tabs { get; } = [];
    public Control? Content { get => _content; private set => SetProperty(ref _content, value); }
    public string Address { get => _address; set => SetProperty(ref _address, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasBrowser => _view is not null;
    public BrowserTabInfo? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value) && !_updatingTabs && value is not null)
            {
                _ = SwitchTabAsync(value);
            }
        }
    }
    public void AttachSession(IBrowserSession session)
    {
        _session = session;
        NotifyCommandState();
        _ = RefreshTabsAsync();
    }
    public void DetachSession(IBrowserSession session)
    {
        if (!ReferenceEquals(_session, session))
        {
            return;
        }

        _session = null;
        Tabs.Clear();
        SelectedTab = null;
        NotifyCommandState();
    }
    public void Attach(WebViewControl view)
    {
        DetachCurrentEvents();
        _view = view;
        _view.NavigationCompleted += HandleNavigationCompleted;
        _view.NavigationHistoryChanged += HandleNavigationHistoryChanged;
        Content = view;
        Status = "内置 WebView2 已就绪，可随时接管操作。";
        NotifyCommandState();
        OnPropertyChanged(nameof(HasBrowser));
    }

    public void Detach(WebViewControl view)
    {
        if (!ReferenceEquals(_view, view))
        {
            return;
        }

        DetachCurrentEvents();
        _view = null;
        Content = null;
        Status = "浏览器会话已关闭。";
        NotifyCommandState();
        OnPropertyChanged(nameof(HasBrowser));
    }

    public async Task RefreshTabsAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        try
        {
            var tabs = await session.ListTabsAsync(CancellationToken.None);
            await AvaloniaUiThread.RunAsync(() => ApplyTabs(tabs), CancellationToken.None);
        }
        catch (Exception exception)
        {
            await AvaloniaUiThread.RunAsync(
                () => Status = $"刷新标签页失败：{exception.Message}", CancellationToken.None);
        }
    }

    private async Task NavigateAsync()
    {
        if (_view is null || !TryCreateAddress(out var uri))
        {
            Status = "请输入有效的 http 或 https 地址。";
            return;
        }

        await AvaloniaUiThread.RunAsync(() => _view.Navigate(uri), CancellationToken.None);
    }

    private Task GoBackAsync() => RunViewActionAsync(static view => view.GoBack());
    private Task GoForwardAsync() => RunViewActionAsync(static view => view.GoForward());
    private Task ReloadAsync() => RunViewActionAsync(static view => view.Reload());
    private Task OpenDevToolsAsync() => RunViewActionAsync(static view => view.OpenDevToolsWindow());

    private async Task NewTabAsync()
    {
        if (_session is null)
        {
            return;
        }

        await _session.NewTabAsync(null, CancellationToken.None);
    }

    private async Task CloseTabAsync()
    {
        if (_session is null || SelectedTab is null)
        {
            return;
        }

        try
        {
            await _session.CloseTabAsync(SelectedTab.Id, CancellationToken.None);
        }
        catch (InvalidOperationException exception)
        {
            Status = exception.Message;
        }
    }

    private async Task SwitchTabAsync(BrowserTabInfo tab)
    {
        if (_session is null)
        {
            return;
        }

        await _session.SwitchTabAsync(tab.Id, CancellationToken.None);
    }

    private void ApplyTabs(IReadOnlyList<BrowserTabInfo> tabs)
    {
        _updatingTabs = true;
        try
        {
            Tabs.ReplaceWith(tabs);
            SelectedTab = Tabs.FirstOrDefault(static tab => tab.IsActive);
        }
        finally
        {
            _updatingTabs = false;
        }

        CloseTabCommand.NotifyCanExecuteChanged();
    }

    private Task RunViewActionAsync(Action<WebViewControl> action)
    {
        var view = _view;
        return view is null
            ? Task.CompletedTask
            : AvaloniaUiThread.RunAsync(() => action(view), CancellationToken.None);
    }

    private bool TryCreateAddress(out Uri uri)
    {
        var value = Address.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = $"https://{value}";
        }

        return Uri.TryCreate(value, UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private void HandleNavigationCompleted(
        object? sender,
        NativeWebViewNavigationCompletedEventArgs args)
    {
        Address = args.Uri?.ToString() ?? Address;
        Status = args.IsSuccess ? "页面加载完成。" : $"加载失败：{args.Error}";
    }

    private void HandleNavigationHistoryChanged(
        object? sender,
        NativeWebViewNavigationHistoryChangedEventArgs args) =>
        NotifyCommandState();

    private bool HasView() => _view is not null;
    private bool HasSession() => _session is not null;
    private bool CanCloseTab() => _session is not null && Tabs.Count > 1 && SelectedTab is not null;
    private bool CanGoBack() => _view?.CanGoBack == true;
    private bool CanGoForward() => _view?.CanGoForward == true;

    private void NotifyCommandState()
    {
        NavigateCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        DevToolsCommand.NotifyCanExecuteChanged();
        NewTabCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
    }

    private void DetachCurrentEvents()
    {
        if (_view is null)
        {
            return;
        }

        _view.NavigationCompleted -= HandleNavigationCompleted;
        _view.NavigationHistoryChanged -= HandleNavigationHistoryChanged;
    }
}
