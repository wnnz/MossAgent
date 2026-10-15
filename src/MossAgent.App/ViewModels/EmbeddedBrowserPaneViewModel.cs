using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Controls;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;
using NativeWebView.Core;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.ViewModels;

public sealed class EmbeddedBrowserPaneViewModel : ObservableObject, IDisposable
{
    private WebViewControl? _view;
    private EventHandler<NativeWebViewNavigationCompletedEventArgs>? _navigationCompletedHandler;
    private EventHandler<NativeWebViewNavigationHistoryChangedEventArgs>? _navigationHistoryHandler;
    private IBrowserSession? _session;
    private Control? _content;
    private BrowserTabInfo? _selectedTab;
    private bool _updatingTabs;
    private bool _disposed;
    private long _tabRefreshVersion;
    private string _address = "https://example.com";
    private string _status = WorkspaceText.Get("BrowserNotStarted");

    public EmbeddedBrowserPaneViewModel(AppBrowserHost? host = null, IToolExecutor? executor = null,
        Guid? taskId = null, TimeSpan? actionTimeout = null)
    {
        Host = host;
        Actions = new(taskId, executor, actionTimeout);
        Actions.StateChanged += HandleActionStateChanged;
        NavigateCommand = new GuardedAsyncRelayCommand(NavigateAsync, HasView);
        BackCommand = new GuardedAsyncRelayCommand(token => ExecuteAsync("browser.back", new { }, token), CanGoBack);
        ForwardCommand = new GuardedAsyncRelayCommand(token => ExecuteAsync("browser.forward", new { }, token), CanGoForward);
        ReloadCommand = new GuardedAsyncRelayCommand(token => ExecuteAsync("browser.reload", new { }, token), HasView);
        DevToolsCommand = new GuardedAsyncRelayCommand(token => ExecuteAsync("browser.devtools", new { }, token),
            () => HasView() && _session?.SupportsDeveloperTools == true);
        NewTabCommand = new GuardedAsyncRelayCommand(token => ExecuteTabAsync("browser.new_tab", new { }, token), HasSession);
        CloseTabCommand = new GuardedAsyncRelayCommand(token =>
            ExecuteTabAsync("browser.close_tab", new { tabId = SelectedTab!.Id }, token), CanCloseTab);
        SwitchTabCommand = new GuardedAsyncRelayCommand<BrowserTabInfo>((tab, token) =>
            ExecuteTabAsync("browser.switch_tab", new { tabId = tab!.Id }, token),
            tab => HasSession() && tab is not null && tab.Id != SelectedTab?.Id && Tabs.Any(item => item.Id == tab.Id));
    }

    public IAsyncRelayCommand NavigateCommand { get; }
    public IAsyncRelayCommand BackCommand { get; }
    public IAsyncRelayCommand ForwardCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IAsyncRelayCommand DevToolsCommand { get; }
    public IAsyncRelayCommand NewTabCommand { get; }
    public IAsyncRelayCommand CloseTabCommand { get; }
    public IAsyncRelayCommand<BrowserTabInfo> SwitchTabCommand { get; }
    public BrowserPaneActionsViewModel Actions { get; }
    public ObservableCollection<BrowserTabInfo> Tabs { get; } = [];
    public AppBrowserHost? Host { get; }
    internal void RegisterView(WebViewControl view) => Host?.Register(view);
    internal void UnregisterView(WebViewControl view) => Host?.Unregister(view);
    public Control? Content { get => _content; private set => SetProperty(ref _content, value); }
    public string Address { get => _address; set => SetProperty(ref _address, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool HasBrowser => _view is not null;
    public BrowserTabInfo? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_updatingTabs) SetProperty(ref _selectedTab, value);
            else if (value != _selectedTab) _ = RequestTabSelectionAsync(value);
        }
    }
    public void AttachContext(ToolExecutionContext? context) => Actions.SetContext(context);
    public void AttachSession(IBrowserSession session)
    {
        _session = session;
        Actions.SetSession(session);
        Interlocked.Increment(ref _tabRefreshVersion);
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
        Actions.SetSession(null);
        Interlocked.Increment(ref _tabRefreshVersion);
        Tabs.Clear();
        _selectedTab = null;
        OnPropertyChanged(nameof(SelectedTab));
        NotifyCommandState();
    }
    public void Attach(WebViewControl view)
    {
        DetachCurrentEvents();
        _view = view;
        _navigationCompletedHandler = (_, args) => HandleNavigationCompleted(view, args);
        _navigationHistoryHandler = (_, args) => HandleNavigationHistoryChanged(view, args);
        view.NavigationCompleted += _navigationCompletedHandler;
        view.NavigationHistoryChanged += _navigationHistoryHandler;
        Content = view;
        Address = view.CurrentUrl?.ToString() ?? string.Empty;
        Status = WorkspaceText.Get("BrowserReady");
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
        Status = WorkspaceText.Get("BrowserSessionClosed");
        NotifyCommandState();
        OnPropertyChanged(nameof(HasBrowser));
    }

    public Task RefreshTabsAsync() => RefreshTabsCoreAsync(CancellationToken.None);

    private async Task<bool> RefreshTabsCoreAsync(CancellationToken cancellationToken)
    {
        var session = _session;
        if (session is null)
        {
            return false;
        }

        var version = Interlocked.Increment(ref _tabRefreshVersion);
        try
        {
            var tabs = await session.ListTabsAsync(cancellationToken);
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested && IsCurrentRefresh(session, version)) ApplyTabs(tabs);
            }, CancellationToken.None);
            return !cancellationToken.IsCancellationRequested && IsCurrentRefresh(session, version);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception)
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (IsCurrentRefresh(session, version)) Status = WorkspaceText.Get("BrowserTabsRefreshFailed");
            }, CancellationToken.None);
        }
        return false;
    }

    private bool IsCurrentRefresh(IBrowserSession session, long version) =>
        ReferenceEquals(_session, session) && version == Volatile.Read(ref _tabRefreshVersion);

    private Task NavigateAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateAddress(out var uri))
        {
            Status = WorkspaceText.Get("BrowserInvalidAddress");
            return Task.CompletedTask;
        }

        return ExecuteAsync("browser.navigate", new { url = uri.AbsoluteUri }, cancellationToken);
    }

    private Task ExecuteAsync(string toolName, object arguments, CancellationToken cancellationToken) =>
        Actions.ExecuteAsync(toolName, arguments, cancellationToken);

    private Task ExecuteTabAsync(string toolName, object arguments, CancellationToken cancellationToken) =>
        Actions.ExecuteAsync(toolName, arguments, cancellationToken, RefreshTabsCoreAsync);

    private Task RequestTabSelectionAsync(BrowserTabInfo? tab)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) RestoreTabSelection();
        }, DispatcherPriority.Normal);
        return SwitchTabCommand.ExecuteAsync(tab);
    }

    private void RestoreTabSelection()
    {
        var selected = _selectedTab;
        _updatingTabs = true;
        try
        {
            SelectedTab = null;
            SelectedTab = selected;
        }
        finally { _updatingTabs = false; }
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

        NotifyCommandState();
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
        if (!ReferenceEquals(sender, _view)) return;
        Address = args.Uri?.ToString() ?? Address;
        if (!Actions.IsBusy) Status = WorkspaceText.Get(args.IsSuccess ? "BrowserPageLoaded" : "BrowserNavigationFailed");
    }

    private void HandleNavigationHistoryChanged(
        object? sender,
        NativeWebViewNavigationHistoryChangedEventArgs args)
    {
        if (!ReferenceEquals(sender, _view)) return;
        NotifyCommandState();
    }

    private bool HasView() => _view is not null && Actions.CanExecute;
    private bool HasSession() => Actions.CanExecute;
    private bool CanCloseTab() => HasSession() && Tabs.Count > 1 && SelectedTab is not null;
    private bool CanGoBack() => HasView() && _view?.CanGoBack == true;
    private bool CanGoForward() => HasView() && _view?.CanGoForward == true;

    private void HandleActionStateChanged(object? sender, EventArgs args)
    {
        Status = !string.IsNullOrEmpty(Actions.Status) ? Actions.Status
            : WorkspaceText.Get(_session is not null ? "BrowserReady" : "BrowserNotStarted");
        NotifyCommandState();
    }

    private void NotifyCommandState()
    {
        NavigateCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ForwardCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
        DevToolsCommand.NotifyCanExecuteChanged();
        NewTabCommand.NotifyCanExecuteChanged();
        CloseTabCommand.NotifyCanExecuteChanged();
        SwitchTabCommand.NotifyCanExecuteChanged();
    }

    private void DetachCurrentEvents()
    {
        if (_view is null)
        {
            return;
        }

        _view.NavigationCompleted -= _navigationCompletedHandler;
        _view.NavigationHistoryChanged -= _navigationHistoryHandler;
        _navigationCompletedHandler = null;
        _navigationHistoryHandler = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Actions.StateChanged -= HandleActionStateChanged;
        Actions.Dispose();
        DetachCurrentEvents();
        foreach (var command in new[] { NavigateCommand, BackCommand, ForwardCommand, ReloadCommand,
            DevToolsCommand, NewTabCommand, CloseTabCommand, SwitchTabCommand }) ((IDisposable)command).Dispose();
    }
}
