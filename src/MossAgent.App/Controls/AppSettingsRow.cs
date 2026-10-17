using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace MossAgent.App.Controls;

[PseudoClasses(":compact")]
public sealed class AppSettingsRow : Grid
{
    public static readonly StyledProperty<double> MinimumLabelWidthProperty =
        AvaloniaProperty.Register<AppSettingsRow, double>(nameof(MinimumLabelWidth), 160, validate: IsPositive);
    public static readonly StyledProperty<double> ControlWidthProperty =
        AvaloniaProperty.Register<AppSettingsRow, double>(nameof(ControlWidth), 240, validate: IsPositive);
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AppSettingsRow, double>(nameof(Gap), 16,
            validate: value => double.IsFinite(value) && value >= 0);
    public static readonly DirectProperty<AppSettingsRow, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AppSettingsRow, bool>(nameof(IsCompact), row => row.IsCompact);

    private bool _isCompact;

    public AppSettingsRow()
    {
        Classes.Add("app-settings-row");
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        RowDefinitions = new RowDefinitions("Auto,0");
    }

    static AppSettingsRow() => AffectsMeasure<AppSettingsRow>(MinimumLabelWidthProperty, ControlWidthProperty, GapProperty);

    public double MinimumLabelWidth { get => GetValue(MinimumLabelWidthProperty); set => SetValue(MinimumLabelWidthProperty, value); }
    public double ControlWidth { get => GetValue(ControlWidthProperty); set => SetValue(ControlWidthProperty, value); }
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
        var compact = double.IsFinite(width) && width < MinimumLabelWidth + ControlWidth + Gap;
        SetAndRaise(IsCompactProperty, ref _isCompact, compact);
        PseudoClasses.Set(":compact", compact);
        ColumnSpacing = compact ? 0 : Gap;
        RowSpacing = compact ? 8 : 0;
        ColumnDefinitions[1].Width = new GridLength(compact ? 0 : ControlWidth);
        RowDefinitions[1].Height = compact ? GridLength.Auto : new GridLength(0);
        for (var index = 0; index < Children.Count; index++)
        {
            SetColumn(Children[index], !compact && index > 0 ? 1 : 0);
            SetRow(Children[index], compact && index > 0 ? 1 : 0);
        }
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;
}
