using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class PathMoveOperation(AuthorizedPathResolver paths,
    IPathMoveSessionFactory sessions, IAuthorizedDirectoryCreator directories)
{
    public Task<ToolResult> ExecuteAsync(string source, string destination, bool overwrite,
        ToolExecutionContext context, CancellationToken token) =>
        Task.Run(() => ExecuteCoreAsync(source, destination, overwrite, context, token), token);

    private async Task<ToolResult> ExecuteCoreAsync(string source, string destination, bool overwrite,
        ToolExecutionContext context, CancellationToken token)
    {
        source = Path.TrimEndingDirectorySeparator(paths.Resolve(source, context));
        destination = Path.TrimEndingDirectorySeparator(paths.Resolve(destination, context));
        await ValidateAsync(source, destination, context, token);
        if (AuthorizedPathResolver.IsSameOrDescendant(destination, source))
            throw new FileMutationSessionException(ToolExecutionText.Get("MoveSelfDenied"), "move_self_denied");
        var observedSource = context.FileSnapshots.Get(context.TaskId, FileSnapshotReader.CacheKey(source));
        var observedTarget = context.FileSnapshots.Get(context.TaskId, FileSnapshotReader.CacheKey(destination));
        using var session = sessions.Create(source, destination, context.AuthorizedRoots, token);
        if (session.FileContent is null && (observedSource is not null || observedTarget?.Exists == true))
            throw new FileEditException("file_changed", "FileChanged");
        if (session.FileContent is not null)
        {
            context.ReadHandleValidator?.Validate(session.FileContent, context.AuthorizedRoots);
            var current = await FileSnapshotReader.HashAsync(session.FileContent, token);
            FileSnapshotReader.ValidateForEdit(source, context, current, null);
        }
        var original = session.FileContent is null ? null
            : await FileSnapshotReader.CaptureForEditAsync(destination, context, null, token);
        if (original?.Exists == true && !overwrite)
            throw new FileMutationSessionException(ToolExecutionText.Get("MoveTargetExists"), "move_target_exists");
        var created = 0;
        try
        {
            using var parent = await directories.AcquireAsync(paths.Resolve(Path.GetDirectoryName(destination)!, context),
                context.AuthorizedRoots, cancellation => ValidateAsync(source, destination, context, cancellation), token);
            created = parent.CreatedCount;
            if (!await session.TryCommitAsync(original?.Sha256,
                cancellation => ValidateAsync(source, destination, context, cancellation), token))
                throw new FileEditException("file_changed", "FileChanged");
            return ToolResult.Success(ToolExecutionText.Format("MoveCompleted", source, destination));
        }
        catch (Exception) when (created > 0 || session.Moved)
        {
            throw new FileMutationSessionException(ToolExecutionText.Format("MoveIncomplete", created, session.Moved ? 1 : 0, source, destination),
                "move_incomplete", true);
        }
        catch (FileWriteSessionException)
        {
            throw new FileMutationSessionException(ToolExecutionText.Get("MoveUnsafe"), "file_move_unsafe");
        }
        finally
        {
            if (session.Moved)
            {
                context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(source));
                context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(destination));
            }
        }
    }

    private async Task ValidateAsync(string source, string destination, ToolExecutionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await context.ValidateWorkspaceAsync(token);
        paths.RejectAuthorizedRootMutation(paths.Resolve(source, context), context);
        paths.RejectAuthorizedRootMutation(paths.Resolve(destination, context), context);
        token.ThrowIfCancellationRequested();
    }
}
