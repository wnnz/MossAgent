using System.Security.Cryptography;
using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Mcp;

internal static class McpToolRegistration
{
    public static IReadOnlyList<IAgentTool> Create(McpServerProfile profile,
        IReadOnlyList<McpToolDefinition> definitions, IMcpClient client, CancellationToken cancellationToken)
    {
        var registrations = new List<IAgentTool>(definitions.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefix = $"mcp.{Normalize(profile.Name)}.{profile.Id:N}.";
        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition.Name)))[..12].ToLowerInvariant();
            var name = $"{prefix}{Normalize(definition.Name)}__{identity}";
            if (!names.Add(name)) throw new InvalidDataException("MCP 服务返回了重复的工具身份。");
            registrations.Add(new McpToolAdapter(name, definition.Name, definition, client));
        }
        return registrations;
    }

    private static string Normalize(string value)
    {
        var characters = value.Select(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_');
        return new string(characters.ToArray()).Trim('_').ToLowerInvariant();
    }
}
