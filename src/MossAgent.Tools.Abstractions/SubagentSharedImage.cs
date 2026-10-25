namespace MossAgent.Tools.Abstractions;

/// <summary>
/// An image input selected from the shared conversation window and carried to
/// a subagent run through <see cref="ToolExecutionContext"/>. Uses primitives
/// only because this contract cannot reference the Application image model.
/// </summary>
public sealed record SubagentSharedImage(string MediaType, int ByteCount, string Base64Data);
