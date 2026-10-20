namespace MossAgent.Tools.Abstractions;

public static class SourceReviewToolPolicy
{
    public static bool IsAllowed(string name) =>
        name.Equals("source.pr_get", StringComparison.OrdinalIgnoreCase)
        || name.Equals("source.pr_files", StringComparison.OrdinalIgnoreCase)
        || name.Equals("source.pr_comments", StringComparison.OrdinalIgnoreCase)
        || name.Equals("source.pr_checks", StringComparison.OrdinalIgnoreCase);
}
