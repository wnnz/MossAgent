using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class GitReferenceDiscoveryProbe : IGitReferenceDiscoveryService
{
    public IReadOnlyList<GitReferenceSuggestion> Items { get; set; } = [];
    public GitReferencePreview Preview { get; set; } =
        new("refs/heads/main", new string('a', 40), "fixture subject", "Fixture Author", DateTimeOffset.UnixEpoch);
    public string? LastQuery { get; private set; }
    public int LastPage { get; private set; }

    public Task<GitReferencePage> ListLocalAsync(ProjectProfile project, string query, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        LastQuery = query;
        LastPage = page;
        var matches = Items.Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var skip = (page - 1) * pageSize;
        return Task.FromResult(new GitReferencePage(
            matches.Skip(skip).Take(pageSize).ToArray(), page, pageSize, matches.Length > skip + pageSize));
    }

    public Task<GitReferencePreview> PreviewAsync(ProjectProfile project, string reference,
        CancellationToken cancellationToken) => Task.FromResult(Preview with { Reference = reference });
}
