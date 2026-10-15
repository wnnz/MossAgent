using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed class AppActionButton : AppButton
{
    private readonly AppText _badge;
    private readonly AppIconLabel _iconLabel;
    private readonly AppIcon _trailingIcon;

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<AppActionButton, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AppActionButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> BadgeTextProperty =
        AvaloniaProperty.Register<AppActionButton, string?>(nameof(BadgeText));

    public static readonly StyledProperty<double> LabelMaxWidthProperty =
        AvaloniaProperty.Register<AppActionButton, double>(nameof(LabelMaxWidth), double.PositiveInfinity);

    public static readonly StyledProperty<Geometry?> TrailingIconDataProperty =
        AvaloniaProperty.Register<AppActionButton, Geometry?>(nameof(TrailingIconData));

    public static readonly StyledProperty<bool> IsTrailingIconVisibleProperty =
        AvaloniaProperty.Register<AppActionButton, bool>(nameof(IsTrailingIconVisible), true);

    public static readonly StyledProperty<double> TrailingIconSpacingProperty =
        AvaloniaProperty.Register<AppActionButton, double>(nameof(TrailingIconSpacing), 3,
            validate: spacing => double.IsFinite(spacing) && spacing >= 0);

    public AppActionButton()
    {
        _badge = new AppText
        {
            Classes = { "subtle", "action-badge" },
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 0, 0)
        };
        _badge.Bind(TextBlock.TextProperty, new Binding(nameof(BadgeText)) { Source = this });
        _iconLabel = new AppIconLabel(this);
        _trailingIcon = new AppIcon
        {
            Classes = { "ui-icon", "icon-micro", "action-trailing-icon" },
            IsVisible = false,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(TrailingIconSpacing, 0, 0, 0)
        };
        Grid.SetColumn(_badge, 1);
        Grid.SetColumn(_trailingIcon, 2);
        Content = new Grid
        {
            Classes = { "action-content" },
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _iconLabel, _badge, _trailingIcon }
        };
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? BadgeText
    {
        get => GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public double LabelMaxWidth
    {
        get => GetValue(LabelMaxWidthProperty);
        set => SetValue(LabelMaxWidthProperty, value);
    }

    public Geometry? TrailingIconData
    {
        get => GetValue(TrailingIconDataProperty);
        set => SetValue(TrailingIconDataProperty, value);
    }

    public bool IsTrailingIconVisible
    {
        get => GetValue(IsTrailingIconVisibleProperty);
        set => SetValue(IsTrailingIconVisibleProperty, value);
    }

    public double TrailingIconSpacing
    {
        get => GetValue(TrailingIconSpacingProperty);
        set => SetValue(TrailingIconSpacingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BadgeTextProperty)
        {
            _badge.IsVisible = !string.IsNullOrWhiteSpace(BadgeText);
        }
        if (change.Property == LabelMaxWidthProperty) _iconLabel.LabelMaxWidth = LabelMaxWidth;
        if (change.Property == TrailingIconDataProperty) _trailingIcon.Data = TrailingIconData;
        if (change.Property == TrailingIconSpacingProperty) _trailingIcon.Margin = new Thickness(TrailingIconSpacing, 0, 0, 0);
        if (change.Property == TrailingIconDataProperty || change.Property == IsTrailingIconVisibleProperty)
            _trailingIcon.IsVisible = TrailingIconData is not null && IsTrailingIconVisible;

        if (change.Property == LabelProperty || change.Property == BadgeTextProperty)
        {
            SetValue(AutomationProperties.NameProperty, string.IsNullOrWhiteSpace(BadgeText)
                ? Label ?? string.Empty
                : $"{Label}，{BadgeText}", BindingPriority.Style);
        }
    }

    protected override Type StyleKeyOverride => typeof(AppButton);
}
