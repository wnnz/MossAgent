using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed class EmptySessionViewportMarginConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Count >= 2 && values[0] is AgentTask && values[1] is Thickness margin
            ? margin
            : new Thickness(0);
}
