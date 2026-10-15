using Avalonia.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.Controls;

public sealed class AppWorkspacePanes : ContentControl
{
    private readonly ColumnDefinition _primaryColumn;
    private readonly ColumnDefinition _inspectorColumn;
    private readonly AppContentControl _inspectorPresenter;
    private readonly AppContentControl _primaryPresenter;
    private readonly AppPaneSplitter _splitter;
    private readonly List<Visual> _ancestors = [];
    private bool _attached;
    private bool _updatingLayout;
    private bool _canDock = true;
    private bool _isInspectorFullWidth;
    private double? _preferredInspectorWidth;

    public static readonly StyledProperty<Control?> PrimaryPaneProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, Control?>(nameof(PrimaryPane));
    public static readonly StyledProperty<Control?> InspectorPaneProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, Control?>(nameof(InspectorPane));
    public static readonly StyledProperty<bool> IsInspectorOpenProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, bool>(nameof(IsInspectorOpen));
    public static readonly StyledProperty<bool> IsInspectorExpandedProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, bool>(nameof(IsInspectorExpanded));
    public static readonly DirectProperty<AppWorkspacePanes, bool> IsInspectorFullWidthProperty =
        AvaloniaProperty.RegisterDirect<AppWorkspacePanes, bool>(nameof(IsInspectorFullWidth), owner => owner.IsInspectorFullWidth);
    public static readonly StyledProperty<double> MinimumPrimaryWidthProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, double>(nameof(MinimumPrimaryWidth), 360, validate: IsValidWidth);
    public static readonly StyledProperty<double> MinimumInspectorWidthProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, double>(nameof(MinimumInspectorWidth), 280, validate: IsValidWidth);
    public static readonly StyledProperty<double> DefaultInspectorFractionProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, double>(nameof(DefaultInspectorFraction), 0.47,
            validate: value => double.IsFinite(value) && value is > 0 and < 1);
    public static readonly StyledProperty<double> FloatingInspectorWidthProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, double>(nameof(FloatingInspectorWidth), 300, validate: IsValidWidth);
    public static readonly StyledProperty<Thickness> FloatingInspectorMarginProperty =
        AvaloniaProperty.Register<AppWorkspacePanes, Thickness>(nameof(FloatingInspectorMargin), new Thickness(0, 58, 8, 0));

    public AppWorkspacePanes()
    {
        ToggleInspectorExpandedCommand = new RelayCommand(ToggleExpansion, CanToggleExpansion);
        ShowInspectorExpandedCommand = new RelayCommand(ShowExpandedInspector, CanShowExpandedInspector);
        Classes.Add("app-workspace-panes");
        _primaryPresenter = CreatePresenter(nameof(PrimaryPane));
        _inspectorPresenter = CreatePresenter(nameof(InspectorPane));
        _splitter = new AppPaneSplitter();
        Grid.SetColumn(_splitter, 1);
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,0"),
            Children = { _primaryPresenter, _splitter, _inspectorPresenter }
        };
        _primaryColumn = layout.ColumnDefinitions[0];
        _inspectorColumn = layout.ColumnDefinitions[2];
        Content = layout;
    }

    public Control? PrimaryPane { get => GetValue(PrimaryPaneProperty); set => SetValue(PrimaryPaneProperty, value); }
    public Control? InspectorPane { get => GetValue(InspectorPaneProperty); set => SetValue(InspectorPaneProperty, value); }
    public bool IsInspectorOpen { get => GetValue(IsInspectorOpenProperty); set => SetValue(IsInspectorOpenProperty, value); }
    public bool IsInspectorExpanded { get => GetValue(IsInspectorExpandedProperty); set => SetValue(IsInspectorExpandedProperty, value); }
    public bool IsInspectorFullWidth => _isInspectorFullWidth;
    public IRelayCommand ToggleInspectorExpandedCommand { get; }
    public IRelayCommand ShowInspectorExpandedCommand { get; }
    public double MinimumPrimaryWidth { get => GetValue(MinimumPrimaryWidthProperty); set => SetValue(MinimumPrimaryWidthProperty, value); }
    public double MinimumInspectorWidth { get => GetValue(MinimumInspectorWidthProperty); set => SetValue(MinimumInspectorWidthProperty, value); }
    public double DefaultInspectorFraction { get => GetValue(DefaultInspectorFractionProperty); set => SetValue(DefaultInspectorFractionProperty, value); }
    public double FloatingInspectorWidth { get => GetValue(FloatingInspectorWidthProperty); set => SetValue(FloatingInspectorWidthProperty, value); }
    public Thickness FloatingInspectorMargin { get => GetValue(FloatingInspectorMarginProperty); set => SetValue(FloatingInspectorMarginProperty, value); }

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
        if ((!IsInspectorOpen || change.Property == InspectorPaneProperty || change.Property == DataContextProperty
            || (_attached && (!IsEffectivelyVisible || !IsEffectivelyEnabled))) && IsInspectorExpanded)
            IsInspectorExpanded = false;
        if (change.Property == IsInspectorOpenProperty || change.Property == IsInspectorExpandedProperty
            || change.Property == IsEnabledProperty || change.Property == IsVisibleProperty)
            ToggleInspectorExpandedCommand?.NotifyCanExecuteChanged();
        if (change.Property == InspectorPaneProperty || change.Property == IsEnabledProperty || change.Property == IsVisibleProperty)
            ShowInspectorExpandedCommand?.NotifyCanExecuteChanged();
        if (change.Property == IsInspectorOpenProperty || change.Property == MinimumPrimaryWidthProperty
            || change.Property == MinimumInspectorWidthProperty || change.Property == DefaultInspectorFractionProperty
            || change.Property == FloatingInspectorWidthProperty || change.Property == FloatingInspectorMarginProperty
            || change.Property == IsInspectorExpandedProperty)
            InvalidateMeasure();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        _inspectorColumn.PropertyChanged += HandleInspectorWidthChanged;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
        NotifyExpansionCommands();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _inspectorColumn.PropertyChanged -= HandleInspectorWidthChanged;
        _attached = false;
        IsInspectorExpanded = false;
        SetAndRaise(IsInspectorFullWidthProperty, ref _isInspectorFullWidth, false);
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        NotifyExpansionCommands();
        base.OnDetachedFromVisualTree(args);
    }

    private void ConfigureLayout(double width)
    {
        _updatingLayout = true;
        try
        {
            var handleWidth = double.IsFinite(_splitter.Width) ? _splitter.Width : 0;
            var canDock = width >= MinimumPrimaryWidth + MinimumInspectorWidth + handleWidth;
            if (_canDock != canDock)
            {
                _canDock = canDock;
                ToggleInspectorExpandedCommand.NotifyCanExecuteChanged();
            }
            var expanded = IsInspectorOpen && (IsInspectorExpanded || !canDock);
            SetAndRaise(IsInspectorFullWidthProperty, ref _isInspectorFullWidth, expanded);
            _primaryPresenter.IsVisible = !expanded;
            _splitter.IsVisible = IsInspectorOpen && !expanded;
            _splitter.IsEnabled = IsInspectorOpen && !expanded;
            _primaryColumn.Width = expanded ? new GridLength(0) : GridLength.Star;
            Grid.SetColumn(_inspectorPresenter, IsInspectorOpen ? 2 : 0);
            _inspectorPresenter.Width = IsInspectorOpen ? double.NaN : Math.Min(FloatingInspectorWidth,
                Math.Max(0, width - FloatingInspectorMargin.Left - FloatingInspectorMargin.Right));
            _inspectorPresenter.HorizontalAlignment = IsInspectorOpen ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
            _inspectorPresenter.Margin = IsInspectorOpen ? default : FloatingInspectorMargin;
            if (IsInspectorOpen && !expanded) ConfigureDockedWidth(width);
            else
            {
                _primaryColumn.MinWidth = 0;
                _inspectorColumn.MinWidth = 0;
                _inspectorColumn.MaxWidth = double.PositiveInfinity;
                _inspectorColumn.Width = expanded ? GridLength.Star : new GridLength(0);
            }
        }
        finally { _updatingLayout = false; }
    }

    private void ConfigureDockedWidth(double width)
    {
        var handleWidth = double.IsFinite(_splitter.Width) ? _splitter.Width : 0;
        var available = Math.Max(0, width - handleWidth);
        var inspectorMinimum = Math.Min(MinimumInspectorWidth, available);
        var primaryMinimum = Math.Min(MinimumPrimaryWidth, Math.Max(0, available - inspectorMinimum));
        var inspectorMaximum = Math.Max(inspectorMinimum, available - primaryMinimum);
        _primaryColumn.MinWidth = primaryMinimum;
        _inspectorColumn.MinWidth = inspectorMinimum;
        _inspectorColumn.MaxWidth = inspectorMaximum;
        _inspectorColumn.Width = new GridLength(Math.Clamp(
            _preferredInspectorWidth ?? width * DefaultInspectorFraction, inspectorMinimum, inspectorMaximum));
    }

    private void HandleInspectorWidthChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (_updatingLayout || !IsInspectorOpen || IsInspectorFullWidth || change.Property != ColumnDefinition.WidthProperty
            || !_inspectorColumn.Width.IsAbsolute) return;
        _preferredInspectorWidth = _inspectorColumn.Width.Value;
        InvalidateMeasure();
    }

    private static bool IsValidWidth(double value) => double.IsFinite(value) && value >= 0;

    private AppContentControl CreatePresenter(string propertyName)
    {
        var presenter = new AppContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        presenter.Bind(ContentProperty, new Binding(propertyName) { Source = this });
        return presenter;
    }

    private bool CanUseInspector() => _attached && IsVisible && IsEnabled && IsEffectivelyVisible && IsEffectivelyEnabled;

    private bool CanToggleExpansion() => _canDock && IsInspectorOpen && CanUseInspector();

    private void ToggleExpansion()
    {
        if (CanToggleExpansion()) IsInspectorExpanded = !IsInspectorExpanded;
    }

    private bool CanShowExpandedInspector() => InspectorPane is not null && CanUseInspector();

    private void ShowExpandedInspector()
    {
        if (!CanShowExpandedInspector()) return;
        SetCurrentValue(IsInspectorOpenProperty, true);
        IsInspectorExpanded = true;
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != IsVisibleProperty && change.Property != IsEnabledProperty) return;
        if (!IsEffectivelyVisible || !IsEffectivelyEnabled) IsInspectorExpanded = false;
        NotifyExpansionCommands();
    }

    private void NotifyExpansionCommands()
    {
        ToggleInspectorExpandedCommand.NotifyCanExecuteChanged();
        ShowInspectorExpandedCommand.NotifyCanExecuteChanged();
    }

    protected override Type StyleKeyOverride => typeof(ContentControl);
}
