using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public sealed record WorkspaceAttachmentContent(
    string FullPath,
    string DisplayPath,
    string Content,
    long ByteCount,
    [property: System.Text.Json.Serialization.JsonIgnore] AttachmentImage? Image = null)
{
    public string MediaType { get; init; } = Image?.MediaType ?? "text/plain";
}
