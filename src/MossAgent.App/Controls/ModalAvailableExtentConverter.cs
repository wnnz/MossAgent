using System.Globalization;
using Avalonia.Data.Converters;

namespace MossAgent.App.Controls;

public sealed class ModalAvailableExtentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var viewportExtent = value is double extent && double.IsFinite(extent)
            ? extent
            : 568d;
        return Math.Max(0d, viewportExtent - 48d);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
