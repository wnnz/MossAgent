using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace MossAgent.App.Controls;

[PseudoClasses(":alternate")]
public sealed class AppIconButton : AppButton
{
    private readonly AppIcon _icon;

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<AppIconButton, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<Geometry?> AlternateIconDataProperty =
        AvaloniaProperty.Register<AppIconButton, Geometry?>(nameof(AlternateIconData));

    public static readonly StyledProperty<bool> IsAlternateIconProperty =
        AvaloniaProperty.Register<AppIconButton, bool>(nameof(IsAlternateIcon));

    public static readonly StyledProperty<bool> IsFilledIconProperty =
        AvaloniaProperty.Register<AppIconButton, bool>(nameof(IsFilledIcon));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AppIconButton, string?>(nameof(Label));

    public AppIconButton()
    {
        Classes.Add("ghost");
        Classes.Add("icon");

        _icon = new AppIcon { Classes = { "ui-icon" }, IsHitTestVisible = false };
        Content = _icon;
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public Geometry? AlternateIconData
    {
        get => GetValue(AlternateIconDataProperty);
        set => SetValue(AlternateIconDataProperty, value);
    }

    public bool IsAlternateIcon
    {
        get => GetValue(IsAlternateIconProperty);
        set => SetValue(IsAlternateIconProperty, value);
    }

    public bool IsFilledIcon
    {
        get => GetValue(IsFilledIconProperty);
        set => SetValue(IsFilledIconProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_icon is null) return;
        if (change.Property == IsAlternateIconProperty) PseudoClasses.Set(":alternate", IsAlternateIcon);

        if (change.Property == IconDataProperty || change.Property == AlternateIconDataProperty
            || change.Property == IsAlternateIconProperty)
        {
            _icon.Data = IsAlternateIcon && AlternateIconData is not null
                ? AlternateIconData : IconData;
        }
        if (change.Property == ForegroundProperty || change.Property == IsFilledIconProperty)
        {
            _icon.Fill = IsFilledIcon ? Foreground : Brushes.Transparent;
            _icon.Stroke = IsFilledIcon ? Brushes.Transparent : Foreground;
        }
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label ?? string.Empty);
            ToolTip.SetTip(this, Label);
        }
    }

    protected override Type StyleKeyOverride => typeof(AppButton);
}
