using System.Security.Cryptography;
using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Attachments;

internal sealed class ConversationDraftAttachmentFiles(AppDataPaths paths, IAttachmentImageDecoder? images,
    IAttachmentDocumentTextExtractor? documents = null)
{
    private readonly AttachmentContentCodec _codec = new(images, documents);

    public async Task<IReadOnlyList<ConversationDraftAttachmentMetadata>> WriteAsync(ConversationDraftScope scope,
        IReadOnlyList<ConversationDraftAttachment> attachments, CancellationToken cancellationToken)
    {
        ConversationDraftAttachmentValidation.Validate(scope, attachments);
        var result = new List<ConversationDraftAttachmentMetadata>();
        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = _codec.Encode(new(attachment.FullPath, attachment.DisplayPath, attachment.Content ?? string.Empty,
                attachment.ByteCount, attachment.Image, attachment.Document));
            if (attachment.Image is not null || attachment.Document is not null)
                await _codec.DecodeAsync(attachment.FullPath, attachment.DisplayPath, bytes, attachment.MediaType, cancellationToken);
            var metadata = new ConversationDraftAttachmentMetadata(scope,
                attachment with { Content = null, Image = null, Document = null },
                Convert.ToHexString(SHA256.HashData(bytes)));
            var path = GetPath(metadata);
            if (File.Exists(path)) await ReadAsync(metadata, cancellationToken);
            else await WriteFileAsync(metadata, bytes, cancellationToken);
            result.Add(metadata);
        }
        return result;
    }

    public async Task<ConversationDraftAttachment> ReadAsync(ConversationDraftAttachmentMetadata metadata, CancellationToken cancellationToken)
    {
        ConversationDraftAttachmentValidation.Validate(metadata.Scope, [metadata.Attachment], requireContent: false);
        if (metadata.Sha256.Length != 64 || !metadata.Sha256.All(char.IsAsciiHexDigit)) throw new InvalidDataException();
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(metadata);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length != metadata.Attachment.ByteCount) throw new InvalidDataException();
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        GetPath(metadata);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException();
        var decoded = await _codec.DecodeAsync(path, metadata.Attachment.DisplayPath, bytes, metadata.Attachment.MediaType, cancellationToken);
        var attachment = metadata.Attachment with
        { Content = decoded.Image is null ? decoded.Content : null, Image = decoded.Image, Document = decoded.Document };
        ConversationDraftAttachmentValidation.Validate(metadata.Scope, [attachment]);
        return attachment;
    }

    private async Task WriteFileAsync(ConversationDraftAttachmentMetadata metadata, byte[] bytes, CancellationToken cancellationToken)
    {
        var path = GetPath(metadata);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        GetPath(metadata);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            GetPath(metadata);
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path)) { await ReadAsync(metadata, cancellationToken); }
            GetPath(metadata);
        }
        finally
        {
            GetPath(metadata);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string GetPath(ConversationDraftAttachmentMetadata metadata)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadata.Scope.Key)));
        var root = Path.GetFullPath(Path.Combine(paths.Root, "draft-attachments"));
        var extension = AttachmentDocument.IsSupportedMediaType(metadata.Attachment.MediaType) ? ".pdf" : ".txt";
        var path = Path.Combine(root, key, metadata.Attachment.Id.ToString("N") + extension);
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
        for (FileSystemInfo? current = new FileInfo(path); current is not null;
             current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
            if (current.LinkTarget is not null || current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException();
        return path;
    }
}
