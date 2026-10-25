using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

// Normalizes the optional single-ref filter of git.ls_remote before it is passed to the
// process: a full refs/ name or the literal HEAD passes through unchanged, any other valid
// branch-name shape is normalized to refs/heads/<name>. Anything Git would not accept as a
// refname (whitespace or control characters, '..', '@{', a leading '-', '@', or more than
// 1024 characters) is rejected during validation so the pattern can never be read as an
// option or inject a second operand.
public static class GitLsRemotePatterns
{
    public static string? Normalize(JsonElement arguments)
    {
        var value = arguments.GetOptionalString("ref");
        if (value is null || value == "HEAD") return value;
        if (value.StartsWith("refs/", StringComparison.Ordinal))
        {
            return GitPushNames.IsBranch(value) ? value : throw Invalid();
        }

        return GitPushNames.IsBranch(value) ? "refs/heads/" + value : throw Invalid();
    }

    private static ArgumentException Invalid() =>
        new(ToolExecutionText.Get("GitLsRemoteSelectionInvalid"));
}
