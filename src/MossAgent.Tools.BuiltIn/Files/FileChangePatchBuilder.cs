using System.Security.Cryptography;
using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Git;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class FileChangePatchBuilder(GitCommandRunner git)
{
    public async Task<ToolArtifact> BuildAsync(string path, byte[] before, byte[] after,
        ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var output = await new FileChangeDiffRunner(git).RunAsync(before, after, false, context, cancellationToken);
        var patch = FileChangePatchHeaders.Rewrite(output, path, context.WorkingDirectory);
        var root = FileChangeSnapshotStorage.DirectoryFor(context);
        return await StoreAsync(root, path, patch, cancellationToken);
    }

    private static async Task<ToolArtifact> StoreAsync(string directory, string path, string patch, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(patch);
        if (bytes.Length > FilePreviewFormat.MaximumTextBytes) throw new FileEditException("snapshot_review_too_large", "ReviewChangeTooLarge");
        var destination = Path.Combine(directory, $"review-{Guid.NewGuid():N}.diff");
        FileChangeSnapshotStorage.RejectLinks(destination);
        try
        {
            await using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await stream.WriteAsync(bytes, cancellationToken);
            FileChangeSnapshotStorage.RejectLinks(destination);
            cancellationToken.ThrowIfCancellationRequested();
            return new(Path.GetFileName(path), destination, "text/x-diff", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch { FileChangeSnapshotStorage.Delete(directory, Path.GetFileName(destination)); throw; }
    }
}
