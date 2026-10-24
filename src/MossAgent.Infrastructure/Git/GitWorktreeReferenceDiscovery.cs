using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Git;

public sealed class GitWorktreeReferenceDiscovery(GitProcessRunner git) : IGitWorktreeReferenceDiscovery
{
    public const int MaximumTake = 50;

    public Task<GitWorktreeReferencePage> SearchAsync(
        ProjectProfile project, string? search, int skip, int take, CancellationToken cancellationToken)
    {
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return Task.Run(() => SearchCoreAsync(snapshot, search, skip, take, cancellationToken), cancellationToken);
    }

    private async Task<GitWorktreeReferencePage> SearchCoreAsync(
        ProjectProfile project, string? search, int skip, int take, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(take, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, MaximumTake);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(search?.Length ?? 0, 512, nameof(search));
        var repository = await new GitWorktreeInspector(git).ReadProjectAsync(project, false, cancellationToken).ConfigureAwait(false);
        var result = await git.RunAsync(repository.Root, repository.Command("for-each-ref",
            "--format=%(refname)%09%(objectname)%09%(objecttype)%09%(*objectname)%09%(*objecttype)%09%(*subject)%09%(subject)",
            "--sort=-creatordate", "--sort=refname", "refs/heads/", "refs/tags/", "refs/remotes/"), cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) throw new InvalidOperationException("Git reference discovery failed.");
        var references = Parse(result.StandardOutput, result.OutputTruncated);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            references = references.Where(reference => reference.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var items = references.Skip(skip).Take(take).ToArray();
        var nextSkip = skip + items.Length;
        var hasMore = references.Count > nextSkip || result.OutputTruncated && items.Length == take;
        return new GitWorktreeReferencePage(items, nextSkip, hasMore, result.OutputTruncated);
    }

    public Task FetchAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return Task.Run(() => FetchCoreAsync(snapshot, cancellationToken), cancellationToken);
    }

    private async Task FetchCoreAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        var repository = await new GitWorktreeInspector(git).ReadProjectAsync(project, false, cancellationToken).ConfigureAwait(false);
        GitWorktreePaths.ValidateRepository(repository);
        var result = await git.RunAsync(repository.Root,
            repository.Command("fetch", "--all", "--tags"), cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) throw new InvalidOperationException("Git remote fetch failed.");
    }

    private static List<GitWorktreeReference> Parse(string output, bool truncated)
    {
        var references = new List<GitWorktreeReference>();
        var lines = output.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0) continue;
            var fields = line.Split('\t');
            if (fields.Length < 7)
            {
                if (truncated && index == lines.Length - 1) break;
                throw new InvalidOperationException("Git reference discovery returned malformed output.");
            }
            GitWorktreeReferenceKind kind;
            string name;
            if (fields[0].StartsWith("refs/heads/", StringComparison.Ordinal)) { kind = GitWorktreeReferenceKind.Branch; name = fields[0][11..]; }
            else if (fields[0].StartsWith("refs/tags/", StringComparison.Ordinal)) { kind = GitWorktreeReferenceKind.Tag; name = fields[0][10..]; }
            else if (fields[0].StartsWith("refs/remotes/", StringComparison.Ordinal)) { kind = GitWorktreeReferenceKind.Remote; name = fields[0][13..]; }
            else continue;
            // Symbolic remote heads (for example refs/remotes/origin/HEAD) add noise without a distinct commit.
            if (name.Length == 0 || kind == GitWorktreeReferenceKind.Remote && name.EndsWith("/HEAD", StringComparison.Ordinal)) continue;
            var commit = fields[3].Length > 0 ? fields[3] : fields[1];
            var objectType = fields[3].Length > 0 ? fields[4] : fields[2];
            if (objectType != "commit" || commit.Length is not (40 or 64) || !commit.All(char.IsAsciiHexDigit)) continue;
            var subject = fields[5].Length > 0 ? fields[5] : string.Join(' ', fields.Skip(6)).Trim();
            references.Add(new(name, fields[0], kind, commit, subject));
        }
        return references;
    }
}
