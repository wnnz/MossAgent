using Avalonia;
using Avalonia.Data;

namespace MossAgent.App.Controls;

public sealed partial class AppThemeColorInput : AppUserControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<AppThemeColorInput, string?>(
        nameof(Text), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<AppThemeColorInput, string?>(nameof(Label));

    public AppThemeColorInput() => InitializeComponent();

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
