namespace MossAgent.Tools.Abstractions;

public sealed record ToolDialogueMessage(string Role, string Content, bool IsPartial = false);
