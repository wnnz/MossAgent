using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class PathDeletion(AuthorizedPathResolver paths, IPathDeleteSessionFactory? sessions)
{
    public Task<int> DeleteAsync(string path, bool recursive, FileSnapshot? expected,
        ToolExecutionContext context, CancellationToken token) =>
        Task.Run(() => DeleteCoreAsync(path, recursive, expected, context, token), token);

    private async Task<int> DeleteCoreAsync(string path, bool recursive, FileSnapshot? expected,
        ToolExecutionContext context, CancellationToken token)
    {
        path = Path.TrimEndingDirectorySeparator(paths.Resolve(path, context));
        paths.RejectAuthorizedRootMutation(path, context);
        _ = context.FileSnapshots.Get(context.TaskId, FileSnapshotReader.CacheKey(path));
        await ValidateAsync(path, context, token);
        if (sessions is null) return await DeletePortableAsync(path, recursive, expected, context, token);
        using var session = sessions.Create(path, context.AuthorizedRoots, recursive, expected is not null, token);
        try
        {
            if (expected is not null)
            {
                if (session.FileContent is null) throw new FileEditException("file_changed", "FileChanged");
                var current = await FileSnapshotReader.HashAsync(session.FileContent, token);
                if (current != expected) throw new FileEditException("file_changed", "FileChanged");
                FileSnapshotReader.ValidateForEdit(path, context, current, null);
            }
            await session.DeleteAsync(cancellation => ValidateAsync(path, context, cancellation), token);
            return session.DeletedCount;
        }
        catch (Exception) when (session.DeletedCount > 0)
        {
            throw new FileDeleteSessionException(ToolExecutionText.Format(
                "DeleteIncomplete", session.DeletedCount, session.PlannedCount), "file_delete_incomplete");
        }
        catch (FileWriteSessionException)
        {
            throw new FileDeleteSessionException(ToolExecutionText.Get("DeleteUnsafe"));
        }
        finally
        {
            if (session.DeletedCount > 0) context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(path));
        }
    }

    private async Task ValidateAsync(string path, ToolExecutionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await context.ValidateWorkspaceAsync(token);
        paths.Resolve(path, context);
        paths.RejectAuthorizedRootMutation(path, context);
        token.ThrowIfCancellationRequested();
    }

    // Compatibility for hosts without a native factory. Production Windows always injects one.
    private async Task<int> DeletePortableAsync(string path, bool recursive, FileSnapshot? expected,
        ToolExecutionContext context, CancellationToken token)
    {
        if (expected is not null)
        {
            var current = await FileSnapshotReader.ReadAsync(path, token, context);
            if (!expected.Exists || current != expected) throw new FileEditException("file_changed", "FileChanged");
            FileSnapshotReader.ValidateForEdit(path, context, current, null);
        }
        await ValidateAsync(path, context, token);
        if (File.Exists(path)) File.Delete(path);
        else if (expected is null && Directory.Exists(path)) Directory.Delete(path, recursive);
        else throw new FileDeleteSessionException(ToolExecutionText.Get("DeleteNotFound"), "path_not_found");
        context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(path));
        return 1;
    }
}
