namespace MossAgent.Domain;

public sealed record PullRequestReviewReceipt(long ReviewId, bool Reconciled);
