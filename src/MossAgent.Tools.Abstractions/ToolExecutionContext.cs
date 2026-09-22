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
    IBrowserSession? BrowserSession = null);
