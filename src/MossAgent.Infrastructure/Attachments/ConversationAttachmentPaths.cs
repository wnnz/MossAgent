using MossAgent.Application.Artifacts;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

internal sealed class ConversationAttachmentPaths(ITaskArtifactPaths paths)
{
    public string GetPath(ConversationAttachment attachment)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.GetTaskDirectory(attachment.TaskId)));
        var path = Path.Combine(root, "attachments", attachment.MessageId.ToString("N"), attachment.Id.ToString("N") + ".txt");
        Validate(path, root);
        return path;
    }

    public void Validate(ConversationAttachment attachment) => GetPath(attachment);

    private static void Validate(string path, string root)
    {
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid attachment artifact path.");
        for (FileSystemInfo? current = new FileInfo(path); current is not null;
             current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
        {
            if (current.LinkTarget is not null
                || current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Linked attachment artifact paths are not allowed.");
        }
    }
}
