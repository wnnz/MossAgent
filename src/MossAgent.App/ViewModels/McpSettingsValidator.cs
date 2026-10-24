using System.Text.Json;
using MossAgent.Domain;

using MossAgent.App.Resources;
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
            error = UiText.T("请填写 MCP server 名称。");
            return false;
        }

        if (transport == McpTransportKind.Stdio && string.IsNullOrWhiteSpace(command))
        {
            error = UiText.T("stdio MCP server 必须填写命令。");
            return false;
        }

        if (transport == McpTransportKind.StreamableHttp
            && (!Uri.TryCreate(url, UriKind.Absolute, out endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttp
                && endpoint.Scheme != Uri.UriSchemeHttps
                || endpoint.UserInfo.Length > 0))
        {
            error = UiText.T("Streamable HTTP URL 必须是有效的 HTTP(S) 地址，且不能包含账号或密码。");
            return false;
        }

        try
        {
            if (!HasStringArray(argumentsJson)
                || !HasStringObject(environmentJson)
                || !HasStringObject(headersJson))
            {
                error = UiText.T("参数必须是数组，环境变量和请求头必须是对象；所有值均须为字符串。");
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            error = UiText.T("JSON 配置无效，请检查参数、环境变量和请求头的格式。");
            return false;
        }
    }

    private static bool HasStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Array
            && document.RootElement.EnumerateArray().All(static item => item.ValueKind == JsonValueKind.String);
    }

    private static bool HasStringObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.EnumerateObject().All(static item => item.Value.ValueKind == JsonValueKind.String);
    }
}

