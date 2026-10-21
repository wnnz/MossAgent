using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class PathCopyOperation(AuthorizedPathResolver paths, AtomicTextFileWriter writer,
    IFileTreeReadSessionFactory sourceSessions, IAuthorizedDirectoryCreator directories)
{
    private readonly PathCopyProgress _progress = new();

    public Task<ToolResult> ExecuteAsync(string source, string destination, bool overwrite,
        ToolExecutionContext context, CancellationToken token) =>
        Task.Run(() => ExecuteCoreAsync(source, destination, overwrite, context, token), token);

    private async Task<ToolResult> ExecuteCoreAsync(string source, string destination, bool overwrite,
        ToolExecutionContext context, CancellationToken token)
    {
        source = Path.TrimEndingDirectorySeparator(paths.Resolve(source, context));
        destination = Path.TrimEndingDirectorySeparator(paths.Resolve(destination, context));
        _ = context.FileSnapshots.Get(context.TaskId, FileSnapshotReader.CacheKey(destination));
        if (AuthorizedPathResolver.IsSameOrDescendant(destination, source))
            throw new FileMutationSessionException(ToolExecutionText.Get("CopySelfDenied"), "copy_self_denied");
        await ValidateAsync(source, destination, context, token);
        using var session = sourceSessions.Create(source, context.AuthorizedRoots, token);
        try
        {
            foreach (var entry in session.Entries)
            {
                var target = entry.RelativePath.Length == 0 ? destination : Path.Combine(destination, entry.RelativePath);
                if (!AuthorizedPathResolver.IsSameOrDescendant(target, destination)) throw new UnauthorizedAccessException();
                await ValidateAsync(source, target, context, token);
                if (entry.Stream is null) await CreateDirectoryAsync(source, target, context, token);
                else await CopyFileAsync(source, target, entry.Stream, overwrite, context, token);
            }
            token.ThrowIfCancellationRequested();
            return ToolResult.Success(ToolExecutionText.Format("CopyCompleted", _progress.Files, _progress.Directories, source, destination));
        }
        catch (Exception exception) when (_progress.HasChanges)
        {
            var detail = exception is FileMutationSessionException mutation ? mutation.Message
                : exception is FileEditException edit ? edit.Message : "";
            throw new FileMutationSessionException(ToolExecutionText.Format("CopyIncomplete", _progress.Files, _progress.Directories, destination)
                + (detail.Length == 0 ? "" : " " + detail), "copy_incomplete", true);
        }
    }

    private async Task CopyFileAsync(string source, string target, FileStream input, bool overwrite,
        ToolExecutionContext context, CancellationToken token)
    {
        context.ReadHandleValidator?.Validate(input, context.AuthorizedRoots);
        var original = await FileSnapshotReader.ReadAsync(paths.Resolve(target, context), token, context);
        if (original.Exists && !overwrite)
            throw new FileMutationSessionException(ToolExecutionText.Get("CopyTargetExists"), "copy_target_exists");
        using var parent = await directories.AcquireAsync(paths.Resolve(Path.GetDirectoryName(target)!, context), context.AuthorizedRoots,
            cancellation => ValidateAsync(source, target, context, cancellation), token);
        _progress.Directories += parent.CreatedCount;
        await ValidateAsync(source, target, context, token);
        await writer.WriteAsync(target, original, context, (output, cancellation) => input.CopyToAsync(output, cancellation), token);
        _progress.Files++;
        context.FileSnapshots.Forget(context.TaskId, FileSnapshotReader.CacheKey(target));
    }

    private async Task CreateDirectoryAsync(string source, string target, ToolExecutionContext context, CancellationToken token)
    {
        _progress.Directories += await directories.CreateAsync(paths.Resolve(target, context), context.AuthorizedRoots,
            cancellation => ValidateAsync(source, target, context, cancellation), token);
    }

    private async Task ValidateAsync(string source, string target, ToolExecutionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await context.ValidateWorkspaceAsync(token);
        paths.Resolve(source, context);
        paths.Resolve(target, context);
        token.ThrowIfCancellationRequested();
    }
}
