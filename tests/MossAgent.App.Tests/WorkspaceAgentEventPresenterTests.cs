using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAgentEventPresenterTests
{
    [Fact]
    public void SubagentProgress_UpdatesOneCardUntilFinalResult()
    {
        var assistant = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        var presented = new List<AgentEvent>();
        var activity = "";
        void Present(AgentEvent agentEvent) =>
            assistant = WorkspaceAgentEventPresenter.ApplyEvent(
                agentEvent, assistant, messages,
                value => activity = value, presented.Add);

        Present(new AgentSubagentProgressEvent(
            "call-1", new ToolDialogueMessage("状态", "模型处理中")));
        var card = messages[1];
        Assert.Equal("运行中", card.ToolStatus);
        Assert.Equal("call-1", card.ToolCallId);

        Present(new AgentSubagentProgressEvent(
            "call-1", new ToolDialogueMessage("工具 · test.read", "读取完成")));
        Assert.Same(card, messages[1]);
        Assert.Equal(2, card.Tool!.Dialogue!.Count);

        Present(new AgentToolEvent("subagent.run", "call-1",
            ToolResult.Success("子代理已完成", "报告",
                [new ToolDialogueMessage("子代理", "报告")])));
        Assert.Single(messages);
        Assert.Same(card, messages[0]);
        Assert.Equal("完成", card.ToolStatus);
        Assert.Equal("报告", Assert.Single(card.Tool!.Dialogue!).Content);
        Assert.Equal(3, presented.Count);
        Assert.Equal("", activity);

        Present(new AgentTextEvent("父代理已汇总"));
        Assert.Equal(2, messages.Count);
        Assert.Same(card, messages[0]);
        Assert.Same(assistant, messages[1]);
        Assert.Equal("父代理已汇总", assistant.Content);
    }

    [Fact]
    public void SubagentPartialReplies_UpdateOneDialogueRowWithoutTouchingOtherCalls()
    {
        var assistant = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        void Present(string callId, ToolDialogueMessage message) =>
            assistant = WorkspaceAgentEventPresenter.ApplyEvent(
                new AgentSubagentProgressEvent(callId, message), assistant, messages, _ => { }, _ => { });

        Present("call-1", new("子代理", "正在", IsPartial: true));
        Present("call-2", new("子代理", "独立任务", IsPartial: true));
        Present("call-1", new("子代理", "正在审查", IsPartial: true));
        var first = Assert.Single(messages, message => message.ToolCallId == "call-1");
        var second = Assert.Single(messages, message => message.ToolCallId == "call-2");
        Assert.Equal("正在审查", Assert.Single(first.Tool!.Dialogue!).Content);
        Assert.Equal("独立任务", Assert.Single(second.Tool!.Dialogue!).Content);
        Assert.True(first.IsToolRunning);
        Assert.True(second.IsToolRunning);
        Present("call-1", new("子代理", "正在审查"));
        Assert.False(Assert.Single(first.Tool.Dialogue!).IsPartial);
    }

    [Fact]
    public void AssistantTextBeforeAndAfterTool_StaysInChronologicalOrder()
    {
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        void Present(AgentEvent agentEvent) =>
            assistant = WorkspaceAgentEventPresenter.ApplyEvent(
                agentEvent, assistant, messages, _ => { }, _ => { });

        Present(new AgentTextEvent("准备检查。"));
        Present(new AgentToolEvent("test.read", "call-1", ToolResult.Success("已读取", "内容")));
        Present(new AgentTextEvent("检查完成。"));

        Assert.Equal(3, messages.Count);
        Assert.Equal("准备检查。", messages[0].Content);
        Assert.True(messages[1].IsTool);
        Assert.Equal("检查完成。", messages[2].Content);
        Assert.Same(assistant, messages[2]);
    }
}
