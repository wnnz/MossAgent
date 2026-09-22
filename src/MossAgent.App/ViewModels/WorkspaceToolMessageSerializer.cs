using System.Text.Json;
using MossAgent.Application.Agent;

namespace MossAgent.App.ViewModels;

public static class WorkspaceToolMessageSerializer
{
    public static string Serialize(AgentToolEvent tool) =>
        JsonSerializer.Serialize(new ToolConversationPayload(
            tool.ToolName,
            tool.Result.IsSuccess,
            tool.Result.Summary,
            tool.Result.Content));

    public static bool TryDeserialize(string content, out ToolConversationPayload? payload)
    {
        try
        {
            payload = JsonSerializer.Deserialize<ToolConversationPayload>(content);
            return payload is not null && !string.IsNullOrWhiteSpace(payload.ToolName);
        }
        catch (JsonException)
        {
            payload = null;
            return false;
        }
    }
}
