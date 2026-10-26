namespace MossAgent.Domain;

// Closed set of pull request merge strategies; the wire mapping lives in the
// source client and default-denies anything outside these three values.
public enum PullRequestMergeMethod
{
    Merge,
    Squash,
    Rebase
}
