using System.Globalization;
using Avalonia.Data.Converters;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed class AuditOutcomeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        InspectorText.Get(value is true ? "Succeeded" : value is false ? "Failed" : "Pending");
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
