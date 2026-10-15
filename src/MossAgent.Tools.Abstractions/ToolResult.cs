namespace MossAgent.Tools.Abstractions;

public sealed record ToolResult(
    bool IsSuccess,
    string Summary,
    string? Content = null,
    IReadOnlyList<ToolArtifact>? Artifacts = null,
    string? ErrorCode = null,
    IReadOnlyList<ToolDialogueMessage>? Dialogue = null,
    IReadOnlyList<ToolFileChange>? FileChanges = null,
    bool FileChangesTruncated = false)
{
    public int OutputCharacterCount =>
        Summary.Length + (Content?.Length ?? 0)
        + (Dialogue?.Sum(static message => message.Role.Length + message.Content.Length) ?? 0)
        + (FileChanges?.Sum(static change => change.OutputCharacterCount) ?? 0);

    public static ToolResult Success(
        string summary,
        string? content = null,
        IReadOnlyList<ToolDialogueMessage>? dialogue = null) =>
        new(true, summary, content, Dialogue: dialogue);

    public static ToolResult Failure(
        string summary,
        string errorCode,
        IReadOnlyList<ToolDialogueMessage>? dialogue = null) =>
        new(false, summary, ErrorCode: errorCode, Dialogue: dialogue);
}
