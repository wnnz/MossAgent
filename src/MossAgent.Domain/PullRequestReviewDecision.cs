namespace MossAgent.Domain;

// Closed set of pull request review conclusions; the wire mapping lives in the
// source client and default-denies anything outside these three values.
public enum PullRequestReviewDecision
{
    Approve,
    RequestChanges,
    Comment
}
