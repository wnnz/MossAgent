using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MossAgent.App.Controls;

public sealed class AppColorSwatch : Border
{
    public static readonly StyledProperty<string?> ColorHexProperty =
        AvaloniaProperty.Register<AppColorSwatch, string?>(nameof(ColorHex));

    public string? ColorHex
    {
        get => GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ColorHexProperty) return;
        Background = ColorHex is { Length: 7 } hex && hex[0] == '#'
            && Color.TryParse(hex, out var color)
            ? new SolidColorBrush(color) : Brushes.Transparent;
    }
}
