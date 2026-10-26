namespace MossAgent.Domain;

public sealed record PullRequestMergeReceipt(string MergeSha, bool Reconciled);
