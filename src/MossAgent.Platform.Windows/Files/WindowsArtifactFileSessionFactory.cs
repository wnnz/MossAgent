using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsArtifactFileSessionFactory(IAtomicFileWriteSessionFactory writes,
    IAuthorizedDirectoryCreator directories) : IArtifactFileSessionFactory
{
    public Task<IArtifactFileSession> CreateAsync(ToolExecutionContext context, string prefix,
        string extension, CancellationToken cancellationToken) =>
        Task.Run(() => CreateCoreAsync(context, prefix, extension, cancellationToken), cancellationToken);

    private async Task<IArtifactFileSession> CreateCoreAsync(ToolExecutionContext context, string prefix,
        string extension, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (context.ArtifactDirectory is null || !Path.IsPathFullyQualified(context.ArtifactDirectory)
            || prefix.Length is < 1 or > 32 || prefix.Any(value => !char.IsAsciiLetterOrDigit(value) && value != '-')
            || extension.Length is < 2 or > 16 || extension[0] != '.' || extension[1..].Any(value => !char.IsAsciiLetterOrDigit(value)))
            throw new UnauthorizedAccessException(WindowsFileText.Get("ArtifactScopeDenied"));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.ArtifactDirectory));
        if (!Path.GetFileName(root).Equals(context.TaskId.ToString("N"), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(WindowsFileText.Get("ArtifactScopeDenied"));
        await context.ValidateWorkspaceAsync(token).ConfigureAwait(false);
        using var lease = await directories.AcquireAsync(root, [Path.GetDirectoryName(root)!],
            context.ValidateWorkspaceAsync, token).ConfigureAwait(false);
        await context.ValidateWorkspaceAsync(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var path = Path.Combine(root, $"{prefix}-{Guid.NewGuid():N}{extension}");
        var session = writes.Create(path, [root], token);
        try { return new WindowsArtifactFileSession(path, session, context); }
        catch { session.Dispose(); throw; }
    }
}
