namespace MossAgent.Domain;

public sealed record ConversationDraftAttachment(Guid Id, string FullPath, string DisplayPath,
    string? Content, long ByteCount, string AuthorizationStamp, string MediaType = "text/plain",
    [property: System.Text.Json.Serialization.JsonIgnore] AttachmentImage? Image = null,
    [property: System.Text.Json.Serialization.JsonIgnore] AttachmentDocument? Document = null)
{
    public bool IsAvailable => MediaType == "text/plain"
        ? Content is not null : Image is not null || Document is not null;
}
