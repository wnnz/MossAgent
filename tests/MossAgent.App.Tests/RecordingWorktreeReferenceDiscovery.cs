using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class RecordingWorktreeReferenceDiscovery(
    Func<string?, int, Task<GitWorktreeReferencePage>>? handler = null) : IGitWorktreeReferenceDiscovery
{
    public List<(string? Search, int Skip, int Take)> Requests { get; } = [];
    public int FetchCalls { get; private set; }
    public Exception? FetchFailure { get; set; }

    public Task<GitWorktreeReferencePage> SearchAsync(ProjectProfile project, string? search, int skip, int take,
        CancellationToken cancellationToken)
    {
        Requests.Add((search, skip, take));
        return handler?.Invoke(search, skip) ?? Task.FromResult(Page());
    }

    public Task FetchAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        FetchCalls++;
        return FetchFailure is null ? Task.CompletedTask : Task.FromException(FetchFailure);
    }

    public static GitWorktreeReference Reference(int index) => new($"ref-{index}", $"refs/heads/ref-{index}",
        GitWorktreeReferenceKind.Branch, new string('a', 40), "subject");

    public static GitWorktreeReferencePage Page(int count = 2, int skip = 0, bool hasMore = false,
        bool truncated = false)
    {
        var items = Enumerable.Range(0, count).Select(index => Reference(skip + index)).ToArray();
        return new GitWorktreeReferencePage(items, skip + count, hasMore, truncated);
    }
}
