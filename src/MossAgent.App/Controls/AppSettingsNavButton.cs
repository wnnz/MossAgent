using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed class AppSettingsNavButton : AppToggleButton
{
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<AppSettingsNavButton, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AppSettingsNavButton, string?>(nameof(Label));

    public AppSettingsNavButton()
    {
        Classes.Add("settings-nav");

        Content = new AppIconLabel(this);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label ?? string.Empty);
        }
    }
}
