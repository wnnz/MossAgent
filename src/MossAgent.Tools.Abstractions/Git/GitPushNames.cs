namespace MossAgent.Tools.Abstractions.Git;

public static class GitPushNames
{
    public static bool IsBranch(string value) => value.Length is > 0 and <= 1024 && value != "@"
        && !value.StartsWith('-') && !value.Contains("..", StringComparison.Ordinal)
        && !value.Contains("@{", StringComparison.Ordinal)
        && !value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || "~^:?*[\\".Contains(character))
        && value.Split('/').All(segment => segment.Length > 0 && !segment.StartsWith('.')
            && !segment.EndsWith('.') && !segment.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));

    public static bool IsRemote(string value) => value.Length <= 256 && IsBranch(value) && !value.Contains('=') && !value.Contains('"');
}
