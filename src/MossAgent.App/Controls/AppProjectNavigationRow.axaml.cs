using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppProjectNavigationRow : AppUserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, string?>(nameof(Title));
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, bool>(nameof(IsSelected));
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, bool>(nameof(IsExpanded));
    public static readonly StyledProperty<object?> ActionTargetProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, object?>(nameof(ActionTarget));
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, ICommand?>(nameof(SelectCommand));
    public static readonly StyledProperty<ICommand?> ToggleExpandedCommandProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, ICommand?>(nameof(ToggleExpandedCommand));
    public static readonly StyledProperty<Control?> SummaryContentProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, Control?>(nameof(SummaryContent));
    public static readonly StyledProperty<bool> IsSummaryOpenProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, bool>(nameof(IsSummaryOpen));
    public static readonly StyledProperty<object?> DragScopeProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, object?>(nameof(DragScope));
    public static readonly StyledProperty<ICommand?> ReorderCommandProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, ICommand?>(nameof(ReorderCommand));
    public static readonly StyledProperty<bool> IsReorderEnabledProperty =
        AvaloniaProperty.Register<AppProjectNavigationRow, bool>(nameof(IsReorderEnabled));
    public static readonly DirectProperty<AppProjectNavigationRow, bool> IsSelectAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppProjectNavigationRow, bool>(nameof(IsSelectAvailable), row => row.IsSelectAvailable);
    public static readonly DirectProperty<AppProjectNavigationRow, bool> IsExpandAvailableProperty =
        AvaloniaProperty.RegisterDirect<AppProjectNavigationRow, bool>(nameof(IsExpandAvailable), row => row.IsExpandAvailable);
    public static readonly DirectProperty<AppProjectNavigationRow, string> DragHintProperty =
        AvaloniaProperty.RegisterDirect<AppProjectNavigationRow, string>(nameof(DragHint), row => row.DragHint);

    private readonly AppFlyout _summaryFlyout;
    private readonly List<Visual> _ancestors = [];
    private bool _isSelectAvailable;
    private bool _isExpandAvailable;
    private bool _dragFailed;
    private string _dragHint = string.Empty;
    private readonly Func<PointerPressedEventArgs, IDataTransfer, Task<DragDropEffects>>? _startDrag;
    private AppProjectDragController? _dragController;

    public AppProjectNavigationRow() : this(null) { }

    internal AppProjectNavigationRow(Func<PointerPressedEventArgs, IDataTransfer, Task<DragDropEffects>>? startDrag)
    {
        _startDrag = startDrag;
        Classes.Add("app-project-navigation-row");
        InitializeComponent();
        _summaryFlyout = (AppFlyout)SelectProjectButton.Flyout!;
        SelectProjectButton.Flyout = null;
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public object? ActionTarget { get => GetValue(ActionTargetProperty); set => SetValue(ActionTargetProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
    public ICommand? ToggleExpandedCommand { get => GetValue(ToggleExpandedCommandProperty); set => SetValue(ToggleExpandedCommandProperty, value); }
    public Control? SummaryContent { get => GetValue(SummaryContentProperty); set => SetValue(SummaryContentProperty, value); }
    public bool IsSummaryOpen { get => GetValue(IsSummaryOpenProperty); set => SetValue(IsSummaryOpenProperty, value); }
    public bool IsSelectAvailable => _isSelectAvailable;
    public bool IsExpandAvailable => _isExpandAvailable;
    public string ToggleLabel => SettingsText.Get("ToggleProjectTasks");
    public object? DragScope { get => GetValue(DragScopeProperty); set => SetValue(DragScopeProperty, value); }
    public ICommand? ReorderCommand { get => GetValue(ReorderCommandProperty); set => SetValue(ReorderCommandProperty, value); }
    public bool IsReorderEnabled { get => GetValue(IsReorderEnabledProperty); set => SetValue(IsReorderEnabledProperty, value); }
    public string DragHint => _dragHint;

    internal void SetDragFailed(bool value)
    {
        if (_dragFailed == value) return;
        _dragFailed = value;
        UpdateDragHint();
    }

    private void UpdateDragHint() => SetAndRaise(DragHintProperty, ref _dragHint,
        _dragFailed ? SidebarText.Get("DragFailed") : Title ?? string.Empty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataContextProperty || change.Property == SelectCommandProperty
            || change.Property == ToggleExpandedCommandProperty
            || (change.Property == IsVisibleProperty && !IsVisible)
            || (change.Property == IsEnabledProperty && !IsEnabled))
        {
            CloseSummary();
            _dragController?.Reset();
        }
        if (change.Property == TitleProperty) UpdateDragHint();
        if (change.Property == ReorderCommandProperty) _dragController?.SynchronizeCommand();
        if (change.Property == DragScopeProperty || change.Property == ActionTargetProperty
            || change.Property == IsReorderEnabledProperty || change.Property == IsEnabledProperty)
            _dragController?.Reset();
        if (change.Property == SelectCommandProperty || change.Property == ToggleExpandedCommandProperty
            || change.Property == ActionTargetProperty)
        {
            SetAndRaise(IsSelectAvailableProperty, ref _isSelectAvailable, SelectCommand is not null && ActionTarget is not null);
            SetAndRaise(IsExpandAvailableProperty, ref _isExpandAvailable, ToggleExpandedCommand is not null && ActionTarget is not null);
            if (change.Property == ActionTargetProperty) CloseSummary();
        }
        if (change.Property == SummaryContentProperty && _summaryFlyout is not null)
        {
            CloseSummary();
            SelectProjectButton.Flyout = SummaryContent is null ? null : _summaryFlyout;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        SelectProjectButton.PropertyChanged += HandleButtonAvailabilityChanged;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
        _dragController ??= new AppProjectDragController(this, SelectProjectButton, _startDrag);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        SelectProjectButton.PropertyChanged -= HandleButtonAvailabilityChanged;
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        _dragController?.Dispose();
        _dragController = null;
        SetDragFailed(false);
        CloseSummary();
        base.OnDetachedFromVisualTree(args);
    }

    private void HandleButtonAvailabilityChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != InputElement.IsEffectivelyEnabledProperty || SelectProjectButton.IsEffectivelyEnabled) return;
        CloseSummary();
        _dragController?.Reset();
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if ((args.Property != IsEnabledProperty && args.Property != IsVisibleProperty)
            || (IsEffectivelyEnabled && IsEffectivelyVisible)) return;
        CloseSummary();
        _dragController?.Reset();
    }

    private void CloseSummary()
    {
        _summaryFlyout?.Hide();
        SetCurrentValue(IsSummaryOpenProperty, false);
    }
}
