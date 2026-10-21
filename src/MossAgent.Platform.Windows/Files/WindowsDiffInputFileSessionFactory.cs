using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsDiffInputFileSessionFactory(IAuthorizedDirectoryCreator directories) : IDiffInputFileSessionFactory
{
    public Task<IDiffInputFileSession> CreateAsync(ToolExecutionContext context, ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken) => Task.Run(() => CreateCoreAsync(context, content, cancellationToken), cancellationToken);

    private async Task<IDiffInputFileSession> CreateCoreAsync(ToolExecutionContext context, ReadOnlyMemory<byte> content, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (content.Length > IDiffInputFileSessionFactory.MaximumBytes)
            throw new FileMutationSessionException(WindowsFileText.Get("DiffInputTooLarge"), "diff_input_too_large");
        if (context.ArtifactDirectory is null || !Path.IsPathFullyQualified(context.ArtifactDirectory))
            throw new UnauthorizedAccessException(WindowsFileText.Get("ArtifactScopeDenied"));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.ArtifactDirectory));
        if (!Path.GetFileName(root).Equals(context.TaskId.ToString("N"), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(WindowsFileText.Get("ArtifactScopeDenied"));
        await context.ValidateWorkspaceAsync(token).ConfigureAwait(false);
        using var creation = await directories.AcquireAsync(root, [Path.GetDirectoryName(root)!], context.ValidateWorkspaceAsync, token).ConfigureAwait(false);
        var path = Path.Combine(root, $"diff-input-{Guid.NewGuid():N}.tmp");
        var lease = WindowsWriteDirectoryLease.Acquire(path, [root], token, false);
        FileStream? stream = null;
        IDiffInputFileSession? session = null;
        try
        {
            await context.ValidateWorkspaceAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var handle = WindowsRelativeFile.Open(lease.Parent, Path.GetFileName(path), WindowsRelativeFile.TemporaryAccess,
                FileShare.Read, 2, false, deleteOnClose: true)!;
            try { stream = new FileStream(handle, FileAccess.ReadWrite, 81920, isAsync: false); }
            catch { handle.Dispose(); throw; }
            session = new WindowsDiffInputFileSession(path, stream, lease);
            new WindowsFileReadHandleValidator().Validate(stream, [root]);
            await stream.WriteAsync(content, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            await context.ValidateWorkspaceAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return session;
        }
        catch
        {
            if (session is not null) session.Dispose();
            else { try { stream?.Dispose(); } finally { lease.Dispose(); } }
            throw;
        }
    }
}
