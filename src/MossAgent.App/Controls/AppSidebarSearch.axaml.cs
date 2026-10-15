using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppSidebarSearch : AppUserControl
{
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<AppSidebarSearch, string>(nameof(Query), string.Empty);
    public static readonly StyledProperty<bool> HasMatchesProperty =
        AvaloniaProperty.Register<AppSidebarSearch, bool>(nameof(HasMatches), true);
    public static readonly StyledProperty<ICommand?> ClearCommandProperty =
        AvaloniaProperty.Register<AppSidebarSearch, ICommand?>(nameof(ClearCommand));
    public static readonly DirectProperty<AppSidebarSearch, bool> HasQueryProperty =
        AvaloniaProperty.RegisterDirect<AppSidebarSearch, bool>(nameof(HasQuery), search => search.HasQuery);
    public static readonly DirectProperty<AppSidebarSearch, bool> ShowEmptyStateProperty =
        AvaloniaProperty.RegisterDirect<AppSidebarSearch, bool>(nameof(ShowEmptyState), search => search.ShowEmptyState);
    public static readonly DirectProperty<AppSidebarSearch, bool> IsClearAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppSidebarSearch, bool>(nameof(IsClearAvailable), search => search.IsClearAvailable);
    public static readonly DirectProperty<AppSidebarSearch, string> HintProperty =
        AvaloniaProperty.RegisterDirect<AppSidebarSearch, string>(nameof(Hint), search => search.Hint);

    private readonly AppFlyout _flyout;
    private readonly List<Visual> _ancestors = [];
    private TopLevel? _host;
    private IInputElement? _returnFocus;
    private bool _attached;
    private bool _hasQuery;
    private bool _showEmptyState;
    private bool _isClearAvailable;
    private string _hint = SidebarText.Get("SearchHint");
    private int _generation;

    public AppSidebarSearch()
    {
        Classes.Add("app-sidebar-search");
        InitializeComponent();
        _flyout = (AppFlyout)SearchTrigger.Flyout!;
        SearchTrigger.Flyout = null;
        SearchTrigger.Click += (_, _) => OpenSearch();
        ClearSearchButton.Click += (_, _) =>
        {
            var generation = _generation;
            Dispatcher.UIThread.Post(() => FocusSearch(generation), DispatcherPriority.Input);
        };
        _flyout.Opened += HandleOpened;
        _flyout.Closed += HandleClosed;
        SearchSurface.AddHandler(KeyDownEvent, HandlePopupKey, RoutingStrategies.Tunnel);
    }

    public string Query { get => GetValue(QueryProperty) ?? string.Empty; set => SetValue(QueryProperty, value ?? string.Empty); }
    public bool HasMatches { get => GetValue(HasMatchesProperty); set => SetValue(HasMatchesProperty, value); }
    public ICommand? ClearCommand { get => GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
    public bool HasQuery => _hasQuery;
    public bool ShowEmptyState => _showEmptyState;
    public bool IsClearAvailable => _isClearAvailable;
    public string Hint => _hint;
    public string SearchLabel => SidebarText.Get(nameof(SearchLabel));
    public string ClearLabel => SidebarText.Get(nameof(ClearLabel));
    public string EmptyLabel => SidebarText.Get(nameof(EmptyLabel));
    public bool IsSearchOpen => _flyout.IsOpen;

    public bool OpenSearch()
    {
        if (!_attached || !IsEffectivelyEnabled || !IsVisible
            || this.GetVisualAncestors().Any(ancestor => !ancestor.IsVisible)) return false;
        if (!_flyout.IsOpen)
        {
            _returnFocus = _host?.FocusManager?.GetFocusedElement();
            _flyout.ShowAt(SearchTrigger);
        }
        else FocusSearch(_generation);
        return true;
    }

    public void CloseSearch() => _flyout.Hide();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QueryProperty || change.Property == HasMatchesProperty || change.Property == ClearCommandProperty)
        {
            SetAndRaise(HasQueryProperty, ref _hasQuery, !string.IsNullOrWhiteSpace(Query));
            SetAndRaise(ShowEmptyStateProperty, ref _showEmptyState, HasQuery && !HasMatches);
            SetAndRaise(IsClearAvailableProperty, ref _isClearAvailable, HasQuery && ClearCommand is not null);
            SetAndRaise(HintProperty, ref _hint, HasQuery
                ? string.Format(CultureInfo.CurrentCulture, SidebarText.Get("ActiveSearchHint"), Query.Trim())
                : SidebarText.Get("SearchHint"));
        }
        if (change.Property == DataContextProperty
            || change.Property == IsEnabledProperty && !IsEnabled || change.Property == IsVisibleProperty && !IsVisible)
        {
            _returnFocus = null;
            _generation++;
            _flyout?.Hide();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        _host = TopLevel.GetTopLevel(this);
        _host?.AddHandler(KeyDownEvent, HandleHostKey, RoutingStrategies.Tunnel);
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        _generation++;
        _returnFocus = null;
        _flyout.Hide();
        _host?.RemoveHandler(KeyDownEvent, HandleHostKey);
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        _host = null;
        base.OnDetachedFromVisualTree(args);
    }

    private void HandleHostKey(object? sender, KeyEventArgs args)
    {
        if (!args.Handled && args.Key == Key.K && args.KeyModifiers == KeyModifiers.Control && OpenSearch()) args.Handled = true;
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if ((args.Property == IsVisibleProperty || args.Property == IsEnabledProperty)
            && (!IsEffectivelyEnabled || _ancestors.Any(ancestor => !ancestor.IsVisible)))
        {
            _returnFocus = null;
            _generation++;
            CloseSearch();
        }
    }

    private void HandlePopupKey(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape || !_flyout.IsOpen) return;
        args.Handled = true;
        CloseSearch();
    }

    private void HandleOpened(object? sender, EventArgs args)
    {
        var generation = ++_generation;
        Dispatcher.UIThread.Post(() => FocusSearch(generation), DispatcherPriority.Input);
    }

    private void FocusSearch(int generation)
    {
        if (!_attached || generation != _generation || !_flyout.IsOpen) return;
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    private void HandleClosed(object? sender, EventArgs args)
    {
        var generation = ++_generation;
        var previous = _returnFocus;
        _returnFocus = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_attached || generation != _generation || _flyout.IsOpen || _host is null
                || previous is not Control control || TopLevel.GetTopLevel(control) != _host
                || !control.IsEffectivelyEnabled || !control.IsVisible
                || control.GetVisualAncestors().Any(ancestor => !ancestor.IsVisible)) return;
            var focused = _host.FocusManager?.GetFocusedElement();
            if (focused is null || ReferenceEquals(focused, SearchTrigger) || ReferenceEquals(focused, SearchInput)) previous.Focus();
        }, DispatcherPriority.Input);
    }
}
