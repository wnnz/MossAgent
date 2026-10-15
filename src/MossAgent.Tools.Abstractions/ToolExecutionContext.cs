using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Tools.Abstractions;

public sealed record ToolExecutionContext(
    Guid TaskId,
    string WorkingDirectory,
    IReadOnlyList<string> AuthorizedRoots,
    ApprovalPolicy ApprovalPolicy,
    int MaximumOutputCharacters = 64 * 1024,
    TimeSpan? Timeout = null,
    string? ArtifactDirectory = null,
    IBrowserSession? BrowserSession = null,
    string? ConversationContext = null,
    ISubagentToolGateway? SubagentTools = null,
    Action<ToolDialogueMessage>? SubagentProgress = null)
{
    public TaskFileSnapshotStore FileSnapshots { get; init; } = new(TaskId);
    public Action<ToolExecutionProgress>? Progress { get; init; }
    public ToolFileChange? ReviewedChange { get; init; }
    public bool ToolsEnabled { get; init; } = true;
}
