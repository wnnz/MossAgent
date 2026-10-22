using System.Collections.Immutable;

namespace MossAgent.Application.Models;

public sealed record ModelMessage(
    ModelRole Role,
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    IReadOnlyList<ModelToolCall>? ToolCalls = null,
    [property: System.Text.Json.Serialization.JsonIgnore] ModelContinuation? Continuation = null,
    [property: System.Text.Json.Serialization.JsonIgnore] ImmutableArray<ModelImage> Images = default,
    [property: System.Text.Json.Serialization.JsonIgnore] ImmutableArray<ModelFile> Files = default);
