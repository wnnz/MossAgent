using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class FilePreviewArtifactWriter(AuthorizedPathResolver paths,
    IAtomicFileWriteSessionFactory sessions, IAuthorizedDirectoryCreator directories)
{
    public Task<ToolArtifact> WriteAsync(string source, byte[] bytes, string mediaType,
        ToolExecutionContext context, CancellationToken token) =>
        Task.Run(() => WriteCoreAsync(source, bytes, mediaType, context, token), token);

    private async Task<ToolArtifact> WriteCoreAsync(string source, byte[] bytes, string mediaType,
        ToolExecutionContext context, CancellationToken token)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.ArtifactDirectory!));
        if (!Path.GetFileName(root).Equals(context.TaskId.ToString("N"), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(ToolExecutionText.Get("ArtifactScopeDenied"));
        var parent = Path.GetDirectoryName(root) ?? throw new UnauthorizedAccessException();
        await ValidateAsync(source, context, token);
        using var directory = await directories.AcquireAsync(root, [parent],
            cancellation => ValidateAsync(source, context, cancellation), token);
        var destination = Path.Combine(root, $"preview-{Guid.NewGuid():N}.snapshot");
        using var session = sessions.Create(destination, [root], token);
        await session.Stream.WriteAsync(bytes, token);
        await ValidateAsync(source, context, token);
        if (!await session.TryCommitAsync(null, token))
            throw new FileMutationSessionException(ToolExecutionText.Get("ArtifactWriteUnsafe"), "artifact_write_conflict");
        return new(Path.GetFileName(source), destination, mediaType, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private async Task ValidateAsync(string source, ToolExecutionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await context.ValidateWorkspaceAsync(token);
        paths.Resolve(source, context);
        token.ThrowIfCancellationRequested();
    }
}
