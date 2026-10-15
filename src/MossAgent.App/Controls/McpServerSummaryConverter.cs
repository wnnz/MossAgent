using System.Globalization;
using Avalonia.Data.Converters;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed class McpServerSummaryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not McpServerProfile profile) return string.Empty;

        if (profile.Transport == McpTransportKind.Stdio) return "标准输入输出 (stdio)";
        if (profile.Url is not { } endpoint) return "Streamable HTTP";

        var port = endpoint.IsDefaultPort ? string.Empty : $":{endpoint.Port}";
        return $"Streamable HTTP · {endpoint.Scheme}://{endpoint.Host}{port}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
