namespace MossAgent.Domain;

// Reconciled marks a receipt proven by the follow-up read after an ambiguous write
// outcome, not by the create response itself.
public sealed record PullRequestCreateReceipt(int Number, string HeadRevision, string BaseRevision, bool Reconciled);
