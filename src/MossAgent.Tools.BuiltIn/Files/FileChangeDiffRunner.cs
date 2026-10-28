using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Git;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class FileChangeDiffRunner(GitCommandRunner git, IDiffInputFileSessionFactory? inputs = null)
{
    public async Task<string> RunAsync(byte[] before, byte[] after, bool statistics,
        ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var original = FileChangeSnapshotText.Decode(before);
        var updated = FileChangeSnapshotText.Decode(after);
        if (inputs is not null)
            return await RunOwnedAsync(original, updated, statistics, context, cancellationToken);
        var root = FileChangeSnapshotStorage.DirectoryFor(context);
        var directory = Path.Combine(root, $"review-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            FileChangeSnapshotStorage.RejectLinks(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "before.txt"), original, new UTF8Encoding(false), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "after.txt"), updated, new UTF8Encoding(false), cancellationToken);
            return await RunGitAsync(Path.Combine(directory, "before.txt"), Path.Combine(directory, "after.txt"), statistics, context, cancellationToken);
        }
        finally
        {
            FileChangeSnapshotStorage.Delete(directory, "before.txt");
            FileChangeSnapshotStorage.Delete(directory, "after.txt");
            try { FileChangeSnapshotStorage.RejectLinks(directory); Directory.Delete(directory); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async Task<string> RunOwnedAsync(string before, string after, bool statistics, ToolExecutionContext context, CancellationToken token)
    {
        using var original = await inputs!.CreateAsync(context, Encoding.UTF8.GetBytes(before), token);
        using var updated = await inputs.CreateAsync(context, Encoding.UTF8.GetBytes(after), token);
        await context.ValidateWorkspaceAsync(token);
        token.ThrowIfCancellationRequested();
        var output = await RunGitAsync(original.Path, updated.Path, statistics, context, token);
        await context.ValidateWorkspaceAsync(token);
        token.ThrowIfCancellationRequested();
        return output;
    }

    private async Task<string> RunGitAsync(string before, string after, bool statistics, ToolExecutionContext context, CancellationToken token)
    {
        var limit = Math.Min(context.MaximumOutputCharacters, statistics ? 1024 : 32_000);
        var result = await git.RunAsync(context.WorkingDirectory,
            ["-c", "core.quotepath=false", "-c", "core.safecrlf=false", "-c", "core.autocrlf=false", "--literal-pathspecs", "diff", "--no-index",
             "--no-ext-diff", "--no-textconv", "--no-color", "--text", statistics ? "--numstat" : "--unified=3", "--", before, after],
            context with { MaximumOutputCharacters = limit }, token, acceptExitCodeOne: true,
            extraReadOnlyPaths: [before, after]);
        if (!result.IsSuccess) throw new FileEditException("snapshot_review_failed", "ReviewChangeFailed");
        if ((result.Content?.Length ?? 0) >= limit) throw new FileEditException("snapshot_review_too_large", "ReviewChangeTooLarge");
        return result.Content ?? string.Empty;
    }
}
