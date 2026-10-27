using System.Text.Json;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Bounded parse of an optional portable root mcp.json. Each property of the
/// mcpServers (or legacy servers) object must be an object with a non-empty string
/// transport 'type'. A leading-dot .mcp.json is not portable and is never read here.</summary>
internal static class PluginMcpManifestParser
{
    public static async Task<IReadOnlyList<PluginMcpServerDeclaration>> ReadAsync(
        IReadOnlyList<PluginArchiveEntry> entries, CancellationToken token)
    {
        var manifest = PluginEntryLookup.FindFile(entries, "mcp.json");
        if (manifest is null) return [];
        var bytes = await PluginEntryReader.ReadAllAsync(manifest, PluginArchiveLimits.McpManifestBytes,
            PluginPackageErrorCode.McpInvalid, token);
        using var document = Parse(bytes);
        var servers = FindServers(document.RootElement);
        var declarations = new List<PluginMcpServerDeclaration>();
        foreach (var server in servers.EnumerateObject())
        {
            if (server.Name.Length == 0 || server.Value.ValueKind != JsonValueKind.Object
                || !server.Value.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(type.GetString()))
                throw Invalid($"MCP server entry '{server.Name}' lacks a declared transport type");
            declarations.Add(new PluginMcpServerDeclaration(server.Name, type.GetString()!));
        }
        return declarations;
    }

    private static JsonElement FindServers(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw Invalid("mcp.json must contain a JSON object");
        var found = root.TryGetProperty("mcpServers", out var servers) || root.TryGetProperty("servers", out servers);
        if (!found || servers.ValueKind != JsonValueKind.Object)
            throw Invalid("mcp.json requires an 'mcpServers' or 'servers' object");
        return servers;
    }

    private static JsonDocument Parse(byte[] bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = PluginArchiveLimits.JsonMaximumDepth });
        }
        catch (JsonException error)
        {
            throw new PluginPackageException(PluginPackageErrorCode.McpInvalid, "mcp.json JSON is malformed", error);
        }
    }

    private static PluginPackageException Invalid(string detail) => new(PluginPackageErrorCode.McpInvalid, detail);
}
