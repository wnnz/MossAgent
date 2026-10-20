using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed record GitPushSnapshot(ToolGitRepository Repository, GitPushPreview Preview, string Protocol);
