using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

/// <summary>
/// The subagent-bound slice of the shared conversation window: the text digest
/// plus the image inputs selected newest-first within the model request limits.
/// </summary>
internal sealed record SubagentSharedContext(
    string Text,
    IReadOnlyList<SubagentSharedImage> Images);
