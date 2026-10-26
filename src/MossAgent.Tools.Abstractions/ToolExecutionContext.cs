using System.Collections.Frozen;
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
    Action<ToolDialogueMessage>? SubagentProgress = null,
    IReadOnlyList<SubagentSharedImage>? SubagentImages = null)
{
    private FrozenSet<string>? _allowedToolNames;

    public TaskFileSnapshotStore FileSnapshots { get; init; } = new(TaskId);
    public Action<ToolExecutionProgress>? Progress { get; init; }
    public ToolFileChange? ReviewedChange { get; init; }
    public bool ToolsEnabled { get; init; } = true;
    public ITaskWorkspaceGuard? WorkspaceGuard { get; init; }
    public Files.IFileReadHandleValidator? ReadHandleValidator { get; init; }
    public SourceRepositoryScope? SourceScope { get; init; }
    public PullRequestRevision? SourceReviewRevision { get; init; }
    public IReadOnlySet<string>? AllowedToolNames
    {
        get => _allowedToolNames;
        init => _allowedToolNames = value?.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsToolAllowed(string name) => ToolsEnabled && (_allowedToolNames is null || _allowedToolNames.Contains(name))
        && (SourceReviewRevision is null || SourceScope is not null && SourceReviewToolPolicy.IsAllowed(name))
        && (SourceScope is null || name.Equals("source.pr_list", StringComparison.OrdinalIgnoreCase)
            || name.Equals("source.pr_get", StringComparison.OrdinalIgnoreCase)
            || name.Equals("source.pr_files", StringComparison.OrdinalIgnoreCase)
            || name.Equals("source.pr_comments", StringComparison.OrdinalIgnoreCase)
            || name.Equals("source.pr_checks", StringComparison.OrdinalIgnoreCase)
            || name.Equals("source.pr_reviews", StringComparison.OrdinalIgnoreCase)
            || name.Equals("source.pr_activity", StringComparison.OrdinalIgnoreCase)
            || (SourceScope.CanWrite && name.Equals("source.pr_comment_create", StringComparison.OrdinalIgnoreCase))
            || (SourceScope.CanWrite && name.Equals("source.pr_review_submit", StringComparison.OrdinalIgnoreCase))
            || (SourceScope.CanWrite && name.Equals("source.pr_close", StringComparison.OrdinalIgnoreCase))
            || (SourceScope.CanWrite && name.Equals("source.pr_merge", StringComparison.OrdinalIgnoreCase)));

    public Task ValidateWorkspaceAsync(CancellationToken cancellationToken) =>
        WorkspaceGuard?.ValidateAsync(this, cancellationToken) ?? Task.CompletedTask;
}
