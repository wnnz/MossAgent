using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed record GitCommitSnapshot(ToolGitRepository Repository, GitCommitPreview Preview);
