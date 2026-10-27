using System.Text;
using System.Text.Json;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Bounded JSON codec for installed-plugin component and MCP declaration metadata.
/// Serialization is fail-closed at 8 KiB UTF-8 per column; the package's own limits already
/// bound the arrays, so this is a second gate, not the only one.</summary>
internal static class InstalledPluginJson
{
    public const int MaximumPayloadBytes = 8 * 1024;

    public static string SerializeComponents(IReadOnlyList<PluginComponentDescriptor> components) =>
        Bound(JsonSerializer.Serialize(components), "components");

    public static string SerializeServers(IReadOnlyList<PluginMcpServerDeclaration> servers) =>
        Bound(JsonSerializer.Serialize(servers), "declared MCP servers");

    public static IReadOnlyList<PluginComponentDescriptor> DeserializeComponents(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PluginComponentDescriptor[]>(json) ?? [];
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Stored plugin component metadata is malformed.", error);
        }
    }

    public static IReadOnlyList<PluginMcpServerDeclaration> DeserializeServers(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PluginMcpServerDeclaration[]>(json) ?? [];
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Stored plugin MCP metadata is malformed.", error);
        }
    }

    private static string Bound(string json, string column)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumPayloadBytes)
            throw new PluginPackageException(PluginPackageErrorCode.InstallFailed,
                $"{column} metadata exceeds its {MaximumPayloadBytes}-byte storage budget");
        return json;
    }
}
