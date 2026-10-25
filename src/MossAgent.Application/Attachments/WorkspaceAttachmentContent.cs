using System.Text.Json.Serialization;
using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public sealed record WorkspaceAttachmentContent(
    string FullPath,
    string DisplayPath,
    string Content,
    long ByteCount,
    [property: JsonIgnore] AttachmentImage? Image = null,
    [property: JsonIgnore] AttachmentDocument? Document = null)
{
    public string MediaType { get; init; } = Image?.MediaType ?? Document?.MediaType ?? "text/plain";
}
