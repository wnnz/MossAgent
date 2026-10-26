namespace MossAgent.Domain;

public sealed record PullRequestBranch(string Name, string Sha)
{
    // Branch/ref names handed to the GitHub API: bounded, no control characters and
    // no ".." (which Git rejects as a ref component). The server still validates the
    // name authoritatively; this only keeps malformed input off the wire.
    public static bool IsValidName(string? value) => value is { Length: >= 1 and <= 255 }
        && !value.Any(char.IsControl) && !value.Contains("..", StringComparison.Ordinal);
}
