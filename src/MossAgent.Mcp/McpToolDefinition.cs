using System.Text.Json;

namespace MossAgent.Mcp;

public sealed record McpToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema,
    bool IsReadOnly);

