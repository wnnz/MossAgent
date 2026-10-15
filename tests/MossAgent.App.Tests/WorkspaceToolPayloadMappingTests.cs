using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceToolPayloadMappingTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public void LiveAndPersistedResults_KeepTheSameRawFields(bool success, bool hasDialogue, bool hasProgress)
    {
        IReadOnlyList<ToolDialogueMessage>? dialogue = hasDialogue
            ? [new("状态", "处理中", true), new("子代理", "```csharp\nvar value = 42;\n```")] : null;
        var toolEvent = new AgentToolEvent("subagent.run", "mapping-call",
            new ToolResult(success, "摘要\n第二行", hasDialogue ? null : "原始详情",
                ErrorCode: success ? null : "tool_timeout", Dialogue: dialogue));
        var assistant = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel>();
        if (hasProgress)
            assistant = WorkspaceAgentEventPresenter.ApplyEvent(new AgentToolProgressEvent(
                toolEvent.ToolName, toolEvent.CallId, ToolExecutionPhase.Executing), assistant, messages, _ => { }, _ => { });
        var priorRow = messages.SingleOrDefault();

        WorkspaceAgentEventPresenter.ApplyEvent(toolEvent, assistant, messages, _ => { }, _ => { });
        Assert.True(WorkspaceToolMessageSerializer.TryDeserialize(
            WorkspaceToolMessageSerializer.Serialize(toolEvent), out var stored));
        var row = Assert.Single(messages);
        var live = Assert.IsType<ToolConversationPayload>(row.Tool);

        if (priorRow is not null) Assert.Same(priorRow, row);
        Assert.Equal(toolEvent.CallId, row.ToolCallId);
        Assert.False(row.IsToolRunning);
        Assert.Null(row.ToolPhase);
        Assert.Same(dialogue, live.Dialogue);
        AssertPayload(toolEvent, live);
        AssertPayload(toolEvent, stored!);
    }

    private static void AssertPayload(AgentToolEvent source, ToolConversationPayload payload)
    {
        Assert.Equal(source.ToolName, payload.ToolName);
        Assert.Equal(source.Result.IsSuccess, payload.IsSuccess);
        Assert.Equal(source.Result.Summary, payload.Summary);
        Assert.Equal(source.Result.Content, payload.Details);
        Assert.Equal(source.Result.ErrorCode, payload.ErrorCode);
        Assert.Equal(source.Result.Dialogue, payload.Dialogue);
    }
}
