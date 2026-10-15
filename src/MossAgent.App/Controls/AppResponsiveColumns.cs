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
        AffectsMeasure<AppResponsiveColumns>(MinimumPrimaryWidthProperty, SecondaryWidthProperty, GapProperty);
    }

    public double MinimumPrimaryWidth { get => GetValue(MinimumPrimaryWidthProperty); set => SetValue(MinimumPrimaryWidthProperty, value); }
    public double SecondaryWidth { get => GetValue(SecondaryWidthProperty); set => SetValue(SecondaryWidthProperty, value); }
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
        var compact = double.IsFinite(width) && width < MinimumPrimaryWidth + SecondaryWidth + Gap;
        SetAndRaise(IsCompactProperty, ref _isCompact, compact);
        PseudoClasses.Set(":compact", compact);
        ColumnSpacing = compact ? 0 : Gap;
        RowSpacing = compact ? Gap : 0;
        ColumnDefinitions[1].Width = new GridLength(compact ? 0 : SecondaryWidth);
        RowDefinitions[1].Height = compact ? GridLength.Star : new GridLength(0);
        for (var index = 0; index < Children.Count; index++)
        {
            SetColumn(Children[index], !compact && index > 0 ? 1 : 0);
            SetRow(Children[index], compact && index > 0 ? 1 : 0);
        }
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;
}
