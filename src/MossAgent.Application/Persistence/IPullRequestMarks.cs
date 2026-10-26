namespace MossAgent.Application.Persistence;

public interface IPullRequestMarks
{
    /// <summary>
    /// Returns the stored viewed marks for the requested pull request numbers of one
    /// account/repository pair. At most 64 numbers may be requested; an empty list yields
    /// an empty result. Marks are per-account bookkeeping: they never authorize reads and
    /// are keyed by the normalized owner/repository of the requesting scope.
    /// </summary>
    Task<IReadOnlyList<PullRequestMark>> GetAsync(Guid accountId, string owner, string repository,
        IReadOnlyList<int> numbers, CancellationToken cancellationToken);

    /// <summary>
    /// Records that the pull request was viewed at the given head commit. Upserts the mark
    /// atomically: the stored head revision and timestamp always move together.
    /// </summary>
    Task MarkViewedAsync(Guid accountId, string owner, string repository, int number,
        string headRevision, CancellationToken cancellationToken);
}
