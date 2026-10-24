using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class StaticWorktreeReferenceDiscovery : IGitWorktreeReferenceDiscovery
{
    public Task<GitWorktreeReferencePage> SearchAsync(ProjectProfile project, string? search, int skip, int take,
        CancellationToken cancellationToken) => Task.FromResult(new GitWorktreeReferencePage(
        [new GitWorktreeReference("ref-0", "refs/heads/ref-0", GitWorktreeReferenceKind.Branch, new string('a', 40), "subject"),
         new GitWorktreeReference("ref-1", "refs/heads/ref-1", GitWorktreeReferenceKind.Branch, new string('b', 40), "subject")],
        2, false, false));

    public Task FetchAsync(ProjectProfile project, CancellationToken cancellationToken) => Task.CompletedTask;
}
