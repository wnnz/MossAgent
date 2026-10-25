namespace MossAgent.Application.Persistence;

public sealed record GitCommitDraftUpdate(Guid TaskId, string Text, long ExpectedRevision);
