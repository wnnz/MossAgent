namespace MossAgent.Tools.Abstractions;

public sealed class ToolRunInterruptedException(
    IEnumerable<ToolDialogueMessage> dialogue, CancellationToken cancellationToken,
    IReadOnlyList<ToolFileChange>? fileChanges = null, bool fileChangesTruncated = false)
    : OperationCanceledException("Tool execution interrupted.", cancellationToken)
{
    public IReadOnlyList<ToolDialogueMessage> Dialogue { get; } = dialogue.ToArray();
    public IReadOnlyList<ToolFileChange>? FileChanges { get; } = fileChanges?.ToArray();
    public bool FileChangesTruncated { get; } = fileChangesTruncated;
}
