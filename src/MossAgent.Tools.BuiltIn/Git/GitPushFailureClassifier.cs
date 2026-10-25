namespace MossAgent.Tools.BuiltIn.Git;

// Classifies bounded git push diagnostics into actionable failure codes without echoing the raw
// output, which may contain the remote URL and embedded credentials. A non-fast-forward rejection
// is recoverable by integrating remote changes first, so it gets a dedicated code and guidance.
public static class GitPushFailureClassifier
{
    public static bool IsNonFastForward(string? content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        return content.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase)
            || content.Contains("fetch first", StringComparison.OrdinalIgnoreCase)
            || content.Contains("stale info", StringComparison.OrdinalIgnoreCase);
    }
}
