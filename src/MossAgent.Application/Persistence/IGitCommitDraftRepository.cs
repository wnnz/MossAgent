namespace MossAgent.Application.Persistence;

public interface IGitCommitDraftRepository
{
    Task<GitCommitDraft?> GetAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Compare-and-swap write: <see cref="GitCommitDraftUpdate.ExpectedRevision"/> 0 asserts that no
    /// row exists yet and inserts; any other value requires the stored revision to match exactly.
    /// Empty <see cref="GitCommitDraftUpdate.Text"/> is the delete path: revision 0 removes any row
    /// unconditionally, while a positive revision only deletes when it still matches.
    /// Returns false when the expected revision does not match the stored state.
    /// </summary>
    Task<bool> SaveAsync(GitCommitDraftUpdate update, CancellationToken cancellationToken);

    /// <summary>expectedRevision 0 deletes unconditionally; positive values compare-and-swap.</summary>
    Task<bool> DeleteAsync(Guid taskId, long expectedRevision, CancellationToken cancellationToken);
}
