using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTerminalPresentationTests
{
    [Theory]
    [InlineData("cancelled", "已取消")]
    [InlineData("failed", "失败")]
    [InlineData("completed", "失败")]
    public void RunEnd_StopsOnlyUnfinishedCardsAndKeepsDialogue(string kind, string expected)
    {
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var running = ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", false,
            "运行中", null, [new ToolDialogueMessage("子代理", "正在检查", true)]), "running", true);
        var completed = ChatMessageViewModel.CreateTool(new ToolConversationPayload("test.read", true, "完成", null));
        var otherTask = ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", false, "另一任务", null),
            "other", true);
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant, running, completed };
        AgentEvent terminal = kind switch
        {
            "cancelled" => new AgentCancelledEvent(),
            "failed" => new AgentFailureEvent("fixture", "模型不可用"),
            _ => new AgentCompletedEvent()
        };

        WorkspaceAgentEventPresenter.ApplyEvent(terminal, assistant, messages, _ => { }, _ => { });

        Assert.False(running.IsToolRunning);
        Assert.Equal(expected, running.ToolStatus);
        Assert.Equal("正在检查", Assert.Single(running.Tool!.Dialogue!).Content);
        Assert.Equal("完成", completed.ToolStatus);
        Assert.True(otherTask.IsToolRunning);
        Assert.DoesNotContain(assistant, messages);
    }

    [Theory]
    [InlineData("tool_cancelled", "已取消")]
    [InlineData("tool_timeout", "超时")]
    [InlineData("tool_failed", "失败")]
    public void FinalToolResult_PreservesItsStatusWhenReopened(string code, string expected)
    {
        var agentEvent = new AgentToolEvent("subagent.run", "call", ToolResult.Failure("已停止", code));
        Assert.True(WorkspaceToolMessageSerializer.TryDeserialize(WorkspaceToolMessageSerializer.Serialize(agentEvent), out var payload));
        Assert.Equal(expected, ChatMessageViewModel.CreateTool(payload!).ToolStatus);
    }

    [Fact]
    public void FinalToolResultWithoutDetails_CollapsesLiveCard()
    {
        var card = ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", false, "运行中", null,
            [new ToolDialogueMessage("子代理", "正在检查", true)]), "call", true);
        Assert.True(card.IsToolExpanded);

        card.UpdateTool(new ToolConversationPayload("subagent.run", false, "已取消", null,
            ErrorCode: "tool_cancelled"), false);

        Assert.False(card.IsToolExpanded);
        Assert.False(card.HasToolDetails);
        Assert.False(card.ToggleToolCommand.CanExecute(null));
    }

    [Fact]
    public void LegacyToolHistory_StillLoadsWithoutErrorCode()
    {
        Assert.True(WorkspaceToolMessageSerializer.TryDeserialize(
            """{"ToolName":"test.read","IsSuccess":true,"Summary":"完成","Details":null}""", out var payload));
        Assert.Null(payload!.ErrorCode);
        Assert.Equal("完成", ChatMessageViewModel.CreateTool(payload).ToolStatus);
    }
}
