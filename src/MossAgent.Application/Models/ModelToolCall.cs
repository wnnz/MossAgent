using System.Text.Json;

namespace MossAgent.Application.Models;

public sealed record ModelToolCall(
    string CallId,
    string Name,
    JsonElement Arguments);

