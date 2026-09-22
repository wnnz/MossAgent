using System.Text.Json;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class McpSettingsValidator
{
    public static bool TryValidate(
        string name,
        McpTransportKind transport,
        string command,
        string url,
        string argumentsJson,
        string environmentJson,
        string headersJson,
        out Uri? endpoint,
        out string error)
    {
        endpoint = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "请填写 MCP server 名称。";
            return false;
        }

        if (transport == McpTransportKind.Stdio && string.IsNullOrWhiteSpace(command))
        {
            error = "stdio MCP server 必须填写命令。";
            return false;
        }

        if (transport == McpTransportKind.StreamableHttp
            && (!Uri.TryCreate(url, UriKind.Absolute, out endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttp
                && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            error = "Streamable HTTP URL 必须是有效的 HTTP(S) 地址。";
            return false;
        }

        try
        {
            if (!HasKind(argumentsJson, JsonValueKind.Array)
                || !HasKind(environmentJson, JsonValueKind.Object)
                || !HasKind(headersJson, JsonValueKind.Object))
            {
                error = "参数必须是数组，环境变量和请求头必须是对象。";
                return false;
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = $"JSON 配置无效：{exception.Message}";
            return false;
        }
    }

    private static bool HasKind(string json, JsonValueKind expected)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == expected;
    }
}
