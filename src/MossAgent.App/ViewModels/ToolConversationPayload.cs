using System.Text.Json.Serialization;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed record ToolConversationPayload(
    string ToolName,
    bool IsSuccess,
    string Summary,
    string? Details,
    IReadOnlyList<ToolDialogueMessage>? Dialogue = null,
    string? ErrorCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ToolFileChange>? FileChanges = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FileChangesTruncated = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SubagentRun? SubagentRun = null)
{
    internal static ToolConversationPayload FromEvent(AgentToolEvent tool) => new(
        tool.ToolName, tool.Result.IsSuccess, tool.Result.Summary,
        tool.Result.Content, tool.Result.Dialogue, tool.Result.ErrorCode,
        tool.Result.FileChanges, tool.Result.FileChangesTruncated, tool.Result.SubagentRun);

    [JsonIgnore]
    public bool HasDialogue => Dialogue is { Count: > 0 };
    [JsonIgnore]
    public bool HasDetails => !HasDialogue && !string.IsNullOrWhiteSpace(Details);
    [JsonIgnore]
    public bool HasSubagentRun => SubagentRun is not null;
    [JsonIgnore]
    public string SubagentRunIdentity => SubagentRun is { } run
        ? $"{run.AgentName} · {run.ProviderName} / {run.ModelId} · {run.StatusLabel}"
        : string.Empty;
}
