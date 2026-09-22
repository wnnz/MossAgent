namespace MossAgent.App.ViewModels;

public sealed record ToolConversationPayload(
    string ToolName,
    bool IsSuccess,
    string Summary,
    string? Details);
