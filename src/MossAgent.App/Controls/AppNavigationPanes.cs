using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.Controls;

[PseudoClasses(":compact", ":overlay-open")]
public sealed class AppNavigationPanes : ContentControl
{
    private readonly AppContentControl _primaryPresenter;
    private readonly AppContentControl _navigationPresenter;
    private readonly AppBorder _dismissSurface;
    private readonly Grid _layout;
    private readonly List<Visual> _ancestors = [];
    private TopLevel? _inputRoot;
    private Control? _previousFocus;
    private Control? _primaryFocus;
    private bool _attached;
    private bool _isCompact;
    private bool _isOverlayOpen;
    private bool _isNavigationVisible;
    private int _focusRevision;

    public static readonly StyledProperty<Control?> PrimaryPaneProperty =
        AvaloniaProperty.Register<AppNavigationPanes, Control?>(nameof(PrimaryPane));
    public static readonly StyledProperty<Control?> NavigationPaneProperty =
        AvaloniaProperty.Register<AppNavigationPanes, Control?>(nameof(NavigationPane));
    public static readonly StyledProperty<bool> IsNavigationOpenProperty =
        AvaloniaProperty.Register<AppNavigationPanes, bool>(nameof(IsNavigationOpen), true, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> NavigationWidthProperty =
        AvaloniaProperty.Register<AppNavigationPanes, double>(nameof(NavigationWidth), 264, validate: IsValidWidth);
    public static readonly StyledProperty<double> MinimumPrimaryWidthProperty =
        AvaloniaProperty.Register<AppNavigationPanes, double>(nameof(MinimumPrimaryWidth), 360, validate: IsValidWidth);
    public static readonly DirectProperty<AppNavigationPanes, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AppNavigationPanes, bool>(nameof(IsCompact), owner => owner.IsCompact);
    public static readonly DirectProperty<AppNavigationPanes, bool> IsOverlayOpenProperty =
        AvaloniaProperty.RegisterDirect<AppNavigationPanes, bool>(nameof(IsOverlayOpen), owner => owner.IsOverlayOpen);
    public static readonly DirectProperty<AppNavigationPanes, bool> IsNavigationVisibleProperty =
        AvaloniaProperty.RegisterDirect<AppNavigationPanes, bool>(nameof(IsNavigationVisible), owner => owner.IsNavigationVisible);
    public static readonly AttachedProperty<bool> DismissOnClickProperty =
        AvaloniaProperty.RegisterAttached<AppNavigationPanes, Control, bool>("DismissOnClick");

    public AppNavigationPanes()
    {
        Classes.Add("app-navigation-panes");
        ToggleNavigationCommand = new RelayCommand(ToggleNavigation, CanToggleNavigation);
        CloseOverlayCommand = new RelayCommand(CloseOverlay, () => IsOverlayOpen && CanToggleNavigation());
        _primaryPresenter = CreatePresenter(nameof(PrimaryPane));
        _navigationPresenter = CreatePresenter(nameof(NavigationPane));
        _navigationPresenter.ZIndex = 2;
        _dismissSurface = new AppBorder { Classes = { "navigation-dismiss-surface" }, ZIndex = 1, IsVisible = false };
        _dismissSurface.PointerPressed += HandleOutsidePointerPressed;
        _layout = new Grid { ColumnDefinitions = new ColumnDefinitions("0,*"), Children = { _primaryPresenter, _dismissSurface, _navigationPresenter } };
        Grid.SetColumnSpan(_dismissSurface, 2);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Content = _layout;
        AddHandler(Button.ClickEvent, HandleNavigationClick);
    }

    public Control? PrimaryPane { get => GetValue(PrimaryPaneProperty); set => SetValue(PrimaryPaneProperty, value); }
    public Control? NavigationPane { get => GetValue(NavigationPaneProperty); set => SetValue(NavigationPaneProperty, value); }
    public bool IsNavigationOpen { get => GetValue(IsNavigationOpenProperty); set => SetValue(IsNavigationOpenProperty, value); }
    public double NavigationWidth { get => GetValue(NavigationWidthProperty); set => SetValue(NavigationWidthProperty, value); }
    public double MinimumPrimaryWidth { get => GetValue(MinimumPrimaryWidthProperty); set => SetValue(MinimumPrimaryWidthProperty, value); }
    public bool IsCompact => _isCompact;
    public bool IsOverlayOpen => _isOverlayOpen;
    public bool IsNavigationVisible => _isNavigationVisible;
    public IRelayCommand ToggleNavigationCommand { get; }
    public IRelayCommand CloseOverlayCommand { get; }
    public static bool GetDismissOnClick(Control control) => control.GetValue(DismissOnClickProperty);
    public static void SetDismissOnClick(Control control, bool value) => control.SetValue(DismissOnClickProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width)) ConfigureLayout(Math.Max(0, availableSize.Width));
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ConfigureLayout(Math.Max(0, finalSize.Width));
        base.MeasureOverride(finalSize);
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_layout is null) return;
        if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty
            || change.Property == NavigationPaneProperty || change.Property == DataContextProperty)
        {
            CloseOverlay();
            NotifyCommands();
        }
        if (change.Property == PrimaryPaneProperty || change.Property == DataContextProperty) _primaryFocus = null;
        if (change.Property == IsNavigationOpenProperty || change.Property == NavigationWidthProperty
            || change.Property == MinimumPrimaryWidthProperty) InvalidateMeasure();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        _inputRoot = TopLevel.GetTopLevel(this);
        _inputRoot?.AddHandler(KeyDownEvent, HandleRootKeyDown);
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
        NotifyCommands();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        _inputRoot?.RemoveHandler(KeyDownEvent, HandleRootKeyDown);
        _inputRoot = null;
        _previousFocus = null;
        _primaryFocus = null;
        CloseOverlay();
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        NotifyCommands();
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        HandleRootKeyDown(this, args);
    }

    private void HandleRootKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Handled || args.Key != Key.Escape || !IsOverlayOpen) return;
        CloseOverlay();
        args.Handled = true;
    }

    protected override void OnGotFocus(FocusChangedEventArgs args)
    {
        base.OnGotFocus(args);
        if (args.Source is not Control focused || !_primaryPresenter.IsVisualAncestorOf(focused)) return;
        if (!IsOverlayOpen) _primaryFocus = focused;
        else
        {
            _navigationPresenter.Focus();
            ScheduleNavigationFocus();
        }
    }

    private void ConfigureLayout(double width)
    {
        var compact = width < NavigationWidth + MinimumPrimaryWidth;
        if (SetAndRaise(IsCompactProperty, ref _isCompact, compact))
        {
            PseudoClasses.Set(":compact", compact);
            CloseOverlay();
        }
        var visible = IsCompact ? IsOverlayOpen : IsNavigationOpen;
        SetAndRaise(IsNavigationVisibleProperty, ref _isNavigationVisible, visible);
        _layout.ColumnDefinitions[0].Width = new GridLength(!IsCompact && visible ? NavigationWidth : 0);
        Grid.SetColumn(_primaryPresenter, IsCompact ? 0 : 1);
        Grid.SetColumnSpan(_primaryPresenter, IsCompact ? 2 : 1);
        Grid.SetColumnSpan(_navigationPresenter, IsCompact ? 2 : 1);
        _navigationPresenter.Width = Math.Min(NavigationWidth, width);
        _navigationPresenter.HorizontalAlignment = HorizontalAlignment.Left;
        _navigationPresenter.IsVisible = visible;
        _primaryPresenter.IsHitTestVisible = !IsOverlayOpen;
        KeyboardNavigation.SetTabNavigation(_primaryPresenter, IsOverlayOpen ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        _navigationPresenter.Focusable = IsOverlayOpen;
        KeyboardNavigation.SetTabNavigation(_navigationPresenter, IsOverlayOpen ? KeyboardNavigationMode.Cycle : KeyboardNavigationMode.Continue);
        _dismissSurface.IsVisible = IsOverlayOpen;
    }

    private void ToggleNavigation()
    {
        if (!CanToggleNavigation()) return;
        if (!IsCompact)
        {
            SetCurrentValue(IsNavigationOpenProperty, !IsNavigationOpen);
            return;
        }
        if (IsOverlayOpen) { CloseOverlay(); return; }
        _previousFocus = _primaryFocus ?? TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        SetAndRaise(IsOverlayOpenProperty, ref _isOverlayOpen, true);
        PseudoClasses.Set(":overlay-open", true);
        ConfigureLayout(Bounds.Width);
        _navigationPresenter.Focus();
        ScheduleNavigationFocus();
        NotifyCommands();
    }

    private void CloseOverlay()
    {
        if (!SetAndRaise(IsOverlayOpenProperty, ref _isOverlayOpen, false)) return;
        _focusRevision++;
        PseudoClasses.Set(":overlay-open", false);
        _dismissSurface.IsVisible = false;
        _navigationPresenter.IsVisible = !IsCompact && IsNavigationOpen;
        SetAndRaise(IsNavigationVisibleProperty, ref _isNavigationVisible, _navigationPresenter.IsVisible);
        _primaryPresenter.IsHitTestVisible = true;
        KeyboardNavigation.SetTabNavigation(_primaryPresenter, KeyboardNavigationMode.Continue);
        _navigationPresenter.Focusable = false;
        KeyboardNavigation.SetTabNavigation(_navigationPresenter, KeyboardNavigationMode.Continue);
        var previous = _previousFocus;
        _previousFocus = null;
        if (_attached && IsEffectivelyVisible && IsEffectivelyEnabled)
        {
            var target = previous is not null && AppPageSwitcher.IsAvailableFocusTarget(previous)
                ? previous : PrimaryPane is { } pane ? AppPageSwitcher.SelectFocusTarget(pane, null) : null;
            if (target is not null && TopLevel.GetTopLevel(target) == TopLevel.GetTopLevel(this)) target.Focus();
        }
        InvalidateMeasure();
        NotifyCommands();
    }

    private void HandleOutsidePointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_dismissSurface).Properties.IsLeftButtonPressed) return;
        CloseOverlay();
        args.Handled = true;
    }

    private void HandleNavigationClick(object? sender, RoutedEventArgs args)
    {
        if (!IsOverlayOpen || args.Source is not Control source) return;
        for (var control = source; control is not null && control != _navigationPresenter; control = control.Parent as Control)
            if (GetDismissOnClick(control)) { CloseOverlay(); return; }
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != IsVisibleProperty && args.Property != IsEnabledProperty) return;
        if (!IsEffectivelyVisible || !IsEffectivelyEnabled) CloseOverlay();
        NotifyCommands();
    }

    private bool CanToggleNavigation() => _attached && IsEffectivelyVisible && IsEffectivelyEnabled && NavigationPane is not null;
    private static bool IsValidWidth(double value) => double.IsFinite(value) && value > 0;

    private void ScheduleNavigationFocus()
    {
        var revision = ++_focusRevision;
        Dispatcher.UIThread.Post(() =>
        {
            if (revision == _focusRevision && IsOverlayOpen && CanToggleNavigation() && NavigationPane is { } pane)
                AppPageSwitcher.SelectFocusTarget(pane, null)?.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void NotifyCommands()
    {
        ToggleNavigationCommand.NotifyCanExecuteChanged();
        CloseOverlayCommand.NotifyCanExecuteChanged();
    }

    private AppContentControl CreatePresenter(string property)
    {
        var presenter = new AppContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        presenter.Bind(ContentProperty, new Binding(property) { Source = this });
        return presenter;
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);
}
