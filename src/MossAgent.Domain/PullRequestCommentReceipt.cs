namespace MossAgent.Domain;

public sealed record PullRequestCommentReceipt(long CommentId, bool Reconciled);
