using MossAgent.Application.Persistence;

namespace MossAgent.App.Tests;

internal sealed class PullRequestMarksProbe : IPullRequestMarks
{
    public List<string> Calls { get; } = [];
    public Func<Guid, string, string, IReadOnlyList<int>, CancellationToken, Task<IReadOnlyList<PullRequestMark>>>? GetHandler;
    public Func<Guid, string, string, int, string, CancellationToken, Task>? MarkHandler;

    public Task<IReadOnlyList<PullRequestMark>> GetAsync(Guid accountId, string owner, string repository,
        IReadOnlyList<int> numbers, CancellationToken cancellationToken)
    {
        Calls.Add($"get:{accountId.ToString("N")[..8]}:{owner}/{repository}:{string.Join(',', numbers)}");
        return GetHandler?.Invoke(accountId, owner, repository, numbers, cancellationToken)
            ?? Task.FromResult<IReadOnlyList<PullRequestMark>>([]);
    }

    public Task MarkViewedAsync(Guid accountId, string owner, string repository, int number,
        string headRevision, CancellationToken cancellationToken)
    {
        Calls.Add($"mark:{accountId.ToString("N")[..8]}:{owner}/{repository}:{number}:{headRevision}");
        return MarkHandler?.Invoke(accountId, owner, repository, number, headRevision, cancellationToken)
            ?? Task.CompletedTask;
    }
}
