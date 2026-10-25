using System.Security.Cryptography;
using System.Text;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Attachments;

internal sealed class ConversationSteeringAttachmentPaths(AppDataPaths paths)
{
    private readonly string _root = Path.GetFullPath(Path.Combine(paths.Root, "draft-attachments"));

    public string GetPath(Guid taskId, ConversationAttachment attachment)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"task:{taskId:D}")));
        var extension = AttachmentDocument.IsSupportedMediaType(attachment.MediaType) ? ".pdf" : ".txt";
        var path = Path.GetFullPath(Path.Combine(_root, key, attachment.Id.ToString("N") + extension));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException();
        AttachmentSnapshotPaths.RejectLinks(path);
        return path;
    }
}
