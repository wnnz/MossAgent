using MossAgent.Application.Git;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeReferenceResolver(GitProcessRunner git)
{
    public async Task<string> ResolveAsync(GitWorktreeRepository repository, string? reference, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (reference is null or "") return repository.Head ?? throw new GitWorktreeReferenceException();
        if (reference.Length > 512 || reference[0] == '-'
            || reference.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw new GitWorktreeReferenceException();
        var result = await git.RunAsync(repository.Root,
            repository.Command("rev-parse", "--verify", "--end-of-options", reference + "^{commit}"), token).ConfigureAwait(false);
        var hash = result.StandardOutput.TrimEnd('\r', '\n');
        if (result.ExitCode != 0 || result.OutputTruncated || hash.Length is not (40 or 64) || !hash.All(char.IsAsciiHexDigit))
            throw new GitWorktreeReferenceException();
        return hash;
    }
}
