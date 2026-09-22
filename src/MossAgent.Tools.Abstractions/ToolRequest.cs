using System.Text.Json;

namespace MossAgent.Tools.Abstractions;

public sealed record ToolRequest(string CallId, string ToolName, JsonElement Arguments);

