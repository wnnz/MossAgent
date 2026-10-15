using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed class AppIconLabel : Grid
{
    private readonly AppIcon _icon;
    private readonly AppText _label;

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<AppIconLabel, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AppIconLabel, string?>(nameof(Label));

    public static readonly StyledProperty<double> LabelMaxWidthProperty =
        AvaloniaProperty.Register<AppIconLabel, double>(nameof(LabelMaxWidth), double.PositiveInfinity);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<AppIconLabel, double>(nameof(Spacing), 5);

    public AppIconLabel() : this(null) { }

    public AppIconLabel(Control? owner)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        VerticalAlignment = VerticalAlignment.Center;
        if (owner is not null) Classes.Add("owned-icon-label");

        _icon = new AppIcon
        {
            Classes = { "ui-icon" },
            IsVisible = false,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Spacing, 0)
        };
        _icon.Bind(IsVisibleProperty, new Binding(nameof(IconData))
        {
            Source = owner ?? this,
            Converter = ObjectConverters.IsNotNull
        });
        if (owner is not null)
        {
            _icon.Bind(AppIcon.DataProperty, new Binding(nameof(IconData)) { Source = owner });
            _icon.Bind(AppIcon.StrokeProperty, new Binding("Foreground") { Source = owner });
        }

        _label = new AppText
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        SetColumn(_label, 1);
        if (owner is not null)
        {
            _label.Bind(TextBlock.TextProperty, new Binding(nameof(Label)) { Source = owner });
            _label.Bind(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = owner });
            _label.Bind(TextBlock.FontSizeProperty, new Binding("FontSize") { Source = owner });
            _label.Bind(TextBlock.FontWeightProperty, new Binding("FontWeight") { Source = owner });
        }
        Children.Add(_icon);
        Children.Add(_label);
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

    public double LabelMaxWidth
    {
        get => GetValue(LabelMaxWidthProperty);
        set => SetValue(LabelMaxWidthProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_icon is null || _label is null) return;

        if (change.Property == IconDataProperty) _icon.Data = IconData;
        if (change.Property == LabelProperty) _label.Text = Label;
        if (change.Property == LabelMaxWidthProperty) _label.MaxWidth = LabelMaxWidth;
        if (change.Property == SpacingProperty) _icon.Margin = new Thickness(0, 0, Spacing, 0);
    }
}
