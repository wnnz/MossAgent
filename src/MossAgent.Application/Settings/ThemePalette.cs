using System.Globalization;

namespace MossAgent.Application.Settings;

public sealed record ThemePalette
{
    private ThemePalette(string background, string foreground, string? accentHex)
    {
        Background = background;
        Foreground = foreground;
        AccentHex = accentHex;
    }

    public string Background { get; }

    public string Foreground { get; }

    public string? AccentHex { get; }

    public static ThemePalette LightDefault { get; } = new("#FFFFFF", "#1A1C1F", null);

    public static ThemePalette DarkDefault { get; } = new("#181818", "#ECECEC", null);

    public static bool TryCreate(string? background, string? foreground, out ThemePalette? palette)
        => TryCreate(background, foreground, null, out palette);

    public static bool TryCreate(string? background, string? foreground, string? accentHex,
        out ThemePalette? palette)
    {
        palette = null;
        if (!IsHexColor(background) || !IsHexColor(foreground)) return false;
        if (ContrastRatio(background!, foreground!) < 4.5) return false;
        if (accentHex is not null
            && (!IsHexColor(accentHex) || ContrastRatio(accentHex, "#FFFFFF") < 4.5)) return false;
        palette = new ThemePalette(background!.ToUpperInvariant(), foreground!.ToUpperInvariant(),
            accentHex?.ToUpperInvariant());
        return true;
    }

    private static bool IsHexColor(string? value) => value is { Length: 7 }
        && value[0] == '#'
        && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out _);

    private static double ContrastRatio(string background, string foreground)
    {
        var backgroundLuminance = Luminance(background);
        var foregroundLuminance = Luminance(foreground);
        return (Math.Max(backgroundLuminance, foregroundLuminance) + 0.05)
            / (Math.Min(backgroundLuminance, foregroundLuminance) + 0.05);
    }

    private static double Luminance(string color)
    {
        var red = LinearChannel(color.AsSpan(1, 2));
        var green = LinearChannel(color.AsSpan(3, 2));
        var blue = LinearChannel(color.AsSpan(5, 2));
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static double LinearChannel(ReadOnlySpan<char> channel)
    {
        var value = byte.Parse(channel, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
