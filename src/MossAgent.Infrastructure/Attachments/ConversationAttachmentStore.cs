using System.Security.Cryptography;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

public sealed class ConversationAttachmentStore(ITaskArtifactPaths paths, TimeProvider? timeProvider = null,
    IAttachmentImageDecoder? images = null, IAttachmentDocumentTextExtractor? documents = null,
    AttachmentSnapshotLeases? leases = null) : IConversationAttachmentStore
{
    private readonly ConversationAttachmentPaths _paths = new(paths);
    private readonly AttachmentContentCodec _codec = new(images, documents);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly AttachmentSnapshotLeases? _leases = leases;

    public Task<IReadOnlyList<ConversationAttachment>> SaveAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> attachments, CancellationToken cancellationToken)
    {
        var selected = attachments.ToArray();
        return RunBoundedAsync(token => SaveCoreAsync(taskId, messageId, selected, token), cancellationToken);
    }

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(Guid taskId, Guid messageId,
        IReadOnlyList<ConversationAttachment> attachments, CancellationToken cancellationToken)
    {
        var selected = attachments.ToArray();
        return RunBoundedAsync(token => ReadCoreAsync(taskId, messageId, selected, token), cancellationToken);
    }

    private async Task<IReadOnlyList<ConversationAttachment>> SaveCoreAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> attachments, CancellationToken cancellationToken)
    {
        if (attachments.Count > WorkspaceAttachmentLimits.MaximumFiles) throw new InvalidDataException("Too many attachments.");
        if (attachments.Where(item => item.Image is not null).Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            throw new InvalidDataException("Image attachments exceed their aggregate byte limit.");
        if (attachments.Where(item => item.Document is not null).Sum(item => item.ByteCount) > AttachmentDocument.MaximumTotalByteCount)
            throw new InvalidDataException("Document attachments exceed their aggregate byte limit.");
        var snapshots = new List<ConversationAttachment>();
        foreach (var content in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = _codec.Encode(content);
            if (content.Image is not null || content.Document is not null)
                await _codec.DecodeAsync(content.FullPath, content.DisplayPath, bytes, content.MediaType, cancellationToken);
            var attachment = new ConversationAttachment(Guid.NewGuid(), taskId, messageId, content.DisplayPath,
                bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)), content.MediaType);
            ConversationAttachmentValidation.Validate(taskId, messageId, [attachment]);
            await WriteAsync(attachment, bytes, cancellationToken);
            snapshots.Add(attachment);
        }
        ConversationAttachmentValidation.Validate(taskId, messageId, snapshots);
        return snapshots;
    }

    private async Task WriteAsync(ConversationAttachment attachment, byte[] bytes, CancellationToken cancellationToken)
    {
        var path = _paths.GetPath(attachment);
        using var lease = _leases?.Acquire(path);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _paths.Validate(attachment);
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
            _paths.Validate(attachment);
            File.Move(temporary, path);
            _paths.Validate(attachment);
        }
        finally
        {
            _paths.Validate(attachment);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadCoreAsync(Guid taskId, Guid messageId,
        IReadOnlyList<ConversationAttachment> attachments, CancellationToken cancellationToken)
    {
        ConversationAttachmentValidation.Validate(taskId, messageId, attachments);
        var contents = new List<WorkspaceAttachmentContent>();
        foreach (var attachment in attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = _paths.GetPath(attachment);
            using var lease = _leases?.Acquire(path);
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != attachment.ByteCount) throw new InvalidDataException("Attachment snapshot length changed.");
            var bytes = new byte[checked((int)attachment.ByteCount)];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            _paths.Validate(attachment);
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(attachment.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Attachment snapshot hash changed.");
            contents.Add(await _codec.DecodeAsync(path, attachment.DisplayPath, bytes, attachment.MediaType, cancellationToken));
        }
        return contents;
    }

    private async Task<T> RunBoundedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), _timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        return await Task.Run(() => operation(cancellation.Token), cancellation.Token).WaitAsync(cancellation.Token);
    }
}
