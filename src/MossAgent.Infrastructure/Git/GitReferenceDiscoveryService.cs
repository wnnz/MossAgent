using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Git;

public sealed class GitReferenceDiscoveryService(
    GitProcessRunner git,
    ProjectAuthorizationMonitor? authorizations = null,
    TimeProvider? timeProvider = null) : IGitReferenceDiscoveryService
{
    private const int MaximumReferences = 5000;
    private readonly GitWorktreeInspector _inspector = new(git);

    public Task<GitReferencePage> ListLocalAsync(ProjectProfile project, string query, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 1000 || pageSize is < 1 or > 50
            || query.Length > 128 || query.Any(char.IsControl))
            throw new ArgumentException("Invalid local Git reference query.");
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return RunAsync(async token =>
        {
            var source = await ReadSourceAsync(snapshot, token).ConfigureAwait(false);
            var result = await git.RunAsync(source.Root,
                ["for-each-ref", "--format=%(refname:short)%09%(refname)%09%(objecttype)%09%(objectname)%09%(*objectname)",
                    $"--count={MaximumReferences + 1}", "refs/heads", "refs/tags"], token,
                1_000_000).ConfigureAwait(false);
            if (result.ExitCode != 0 || result.OutputTruncated)
                throw new InvalidOperationException("Local Git reference discovery failed.");
            var matches = Parse(result.StandardOutput).Where(item =>
                item.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
            var skip = checked((page - 1) * pageSize);
            var items = matches.Skip(skip).Take(pageSize).ToArray();
            return new GitReferencePage(items, page, pageSize, matches.Length > skip + items.Length);
        }, cancellationToken);
    }

    public Task<GitReferencePreview> PreviewAsync(ProjectProfile project, string reference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 512 || reference.Any(char.IsControl))
            throw new ArgumentException("Invalid Git reference.", nameof(reference));
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return RunAsync(async token =>
        {
            var source = await ReadSourceAsync(snapshot, token).ConfigureAwait(false);
            var commit = await new GitWorktreeReferenceResolver(git)
                .ResolveAsync(source, reference, token).ConfigureAwait(false);
            var result = await git.RunAsync(source.Root,
                ["show", "-s", "--no-show-signature", "--format=%H%x00%an%x00%aI%x00%s", commit], token)
                .ConfigureAwait(false);
            if (result.ExitCode != 0 || result.OutputTruncated)
                throw new InvalidOperationException("Git reference preview failed.");
            var fields = result.StandardOutput.TrimEnd('\r', '\n').Split('\0');
            if (fields.Length != 4 || !fields[0].Equals(commit, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Git returned an invalid reference preview.");
            var subject = SafeText(fields[3], 512);
            var author = SafeText(fields[1], 256);
            if (!DateTimeOffset.TryParse(fields[2], out var authoredAt))
                throw new InvalidDataException("Git returned an invalid author date.");
            return new GitReferencePreview(reference, commit, subject, author, authoredAt);
        }, cancellationToken);
    }

    private async Task<GitWorktreeRepository> ReadSourceAsync(ProjectProfile project, CancellationToken token)
    {
        await using var lease = authorizations is null ? null
            : await authorizations.AcquireAsync(project, token).ConfigureAwait(false);
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(token, lease?.Revocation ?? default);
        return await _inspector.ReadProjectAsync(project, false, scope.Token).ConfigureAwait(false);
    }

    private static IEnumerable<GitReferenceSuggestion> Parse(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\t');
            if (fields.Length != 5) continue;
            var kind = fields[1].StartsWith("refs/heads/", StringComparison.Ordinal) ? GitReferenceKind.Branch
                : fields[1].StartsWith("refs/tags/", StringComparison.Ordinal) ? GitReferenceKind.Tag
                : (GitReferenceKind?)null;
            var commit = string.IsNullOrWhiteSpace(fields[4]) ? fields[3] : fields[4];
            if (kind is null || fields[0].Length is 0 or > 512 || commit.Length is not (40 or 64)
                || !commit.All(char.IsAsciiHexDigit)) continue;
            yield return new(fields[0], fields[1], kind.Value, commit);
        }
    }

    private static string SafeText(string value, int maximumLength)
    {
        var sanitized = new string(value.Select(character => char.IsControl(character) ? ' ' : character).ToArray()).Trim();
        return sanitized.Length <= maximumLength ? sanitized : sanitized[..maximumLength];
    }

    private Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            return await action(linked.Token);
        }, cancellationToken);
}
