namespace MossAgent.Domain;

// What the write client returns for a pull request create or create-reconcile read:
// the assigned number plus the head/base commits the server recorded.
public sealed record PullRequestCreatePayload(int Number, string HeadRevision, string BaseRevision);
