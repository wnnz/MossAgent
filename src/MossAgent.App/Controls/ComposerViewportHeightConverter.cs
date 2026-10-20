using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MossAgent.App.Controls;

/// <summary>Keeps conversation space below the floating context card when the composer grows.</summary>
public sealed class ComposerViewportHeightConverter : IMultiValueConverter
{
    private const double HeaderHeight = 52;
    private const double MinimumMessageHeight = 96;

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not Rect bounds || bounds.Height <= 0)
            return double.PositiveInfinity;
        var inset = values[1] is Thickness margin ? margin.Top + margin.Bottom : 0;
        return Math.Max(0, Math.Min(bounds.Height * 0.6, bounds.Height - HeaderHeight - inset - MinimumMessageHeight));
    }
}
