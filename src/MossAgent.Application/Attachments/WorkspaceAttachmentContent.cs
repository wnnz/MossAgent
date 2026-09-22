namespace MossAgent.Application.Attachments;

public sealed record WorkspaceAttachmentContent(
    string FullPath,
    string DisplayPath,
    string Content,
    long ByteCount);
