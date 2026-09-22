using System.Text.Json;

namespace MossAgent.Application.Models;

public sealed record ToolCallCompleted(
    string CallId,
    string ToolName,
    JsonElement Arguments) : ModelEvent;

