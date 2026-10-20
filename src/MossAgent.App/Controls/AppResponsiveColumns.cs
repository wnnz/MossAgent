using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace MossAgent.App.Controls;

[PseudoClasses(":compact")]
public sealed class AppResponsiveColumns : Grid
{
    public static readonly StyledProperty<double> MinimumPrimaryWidthProperty =
        AvaloniaProperty.Register<AppResponsiveColumns, double>(nameof(MinimumPrimaryWidth), 300, validate: IsPositive);
    public static readonly StyledProperty<double> SecondaryWidthProperty =
        AvaloniaProperty.Register<AppResponsiveColumns, double>(nameof(SecondaryWidth), 260, validate: IsPositive);
    public static readonly StyledProperty<double> PrimaryWidthProperty =
        AvaloniaProperty.Register<AppResponsiveColumns, double>(nameof(PrimaryWidth), double.NaN,
            validate: value => double.IsNaN(value) || IsPositive(value));
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AppResponsiveColumns, double>(nameof(Gap), 20, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly DirectProperty<AppResponsiveColumns, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AppResponsiveColumns, bool>(nameof(IsCompact), owner => owner.IsCompact);

    private bool _isCompact;

    public AppResponsiveColumns()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        RowDefinitions = new RowDefinitions("*,0");
    }

    static AppResponsiveColumns()
    {
        AffectsMeasure<AppResponsiveColumns>(MinimumPrimaryWidthProperty, SecondaryWidthProperty, PrimaryWidthProperty, GapProperty);
    }

    public double MinimumPrimaryWidth { get => GetValue(MinimumPrimaryWidthProperty); set => SetValue(MinimumPrimaryWidthProperty, value); }
    public double SecondaryWidth { get => GetValue(SecondaryWidthProperty); set => SetValue(SecondaryWidthProperty, value); }
    public double PrimaryWidth { get => GetValue(PrimaryWidthProperty); set => SetValue(PrimaryWidthProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public bool IsCompact => _isCompact;

    protected override Size MeasureOverride(Size availableSize)
    {
        ConfigureLayout(availableSize.Width);
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ConfigureLayout(finalSize.Width);
        return base.ArrangeOverride(finalSize);
    }

    private void ConfigureLayout(double width)
    {
        var fixedPrimary = double.IsFinite(PrimaryWidth);
        var compact = double.IsFinite(width) && width < MinimumPrimaryWidth + (fixedPrimary ? PrimaryWidth : SecondaryWidth) + Gap;
        SetAndRaise(IsCompactProperty, ref _isCompact, compact);
        PseudoClasses.Set(":compact", compact);
        ColumnSpacing = compact ? 0 : Gap;
        var hasPrimary = Children.Count > 0 && Children[0].IsVisible;
        var hasSecondary = Children.Count > 1 && Children[1].IsVisible;
        RowSpacing = compact && hasPrimary && hasSecondary ? Gap : 0;
        ColumnDefinitions[0].Width = !compact && fixedPrimary ? new GridLength(PrimaryWidth) : GridLength.Star;
        ColumnDefinitions[1].Width = compact ? new GridLength(0) : fixedPrimary ? GridLength.Star : new GridLength(SecondaryWidth);
        RowDefinitions[0].Height = compact && !hasPrimary ? new GridLength(0) : GridLength.Star;
        RowDefinitions[1].Height = compact && hasSecondary ? GridLength.Star : new GridLength(0);
        for (var index = 0; index < Children.Count; index++)
        {
            SetColumn(Children[index], !compact && index > 0 ? 1 : 0);
            SetRow(Children[index], compact && index > 0 ? 1 : 0);
        }
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;
}
