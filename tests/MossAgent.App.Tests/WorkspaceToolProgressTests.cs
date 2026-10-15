using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceToolProgressTests
{
    [Fact]
    public void Phases_UpdateOneRowUntilFinalResultAndPreserveChronologicalReplies()
    {
        var assistant = new ChatMessageViewModel("MossAgent", "准备检查。");
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        void Present(AgentEvent item) => assistant = WorkspaceAgentEventPresenter.ApplyEvent(item, assistant, messages, _ => { }, _ => { });
        ChatMessageViewModel? row = null;
        foreach (var phase in Enum.GetValues<ToolExecutionPhase>())
        {
            Present(new AgentToolProgressEvent("file.read", "tool-call", phase));
            row ??= Assert.Single(messages, message => message.IsTool);
            Assert.Same(row, Assert.Single(messages, message => message.IsTool));
            Assert.Equal(phase, row.ToolPhase);
            Assert.True(row.IsToolRunning);
            Assert.Equal(WorkspaceText.Get($"ToolPhase{phase}"), row.ToolStatus);
            Assert.False(row.HasToolDetails);
            Assert.False(row.ToggleToolCommand.CanExecute(null));
        }
        Present(new AgentToolEvent("file.read", "tool-call", ToolResult.Success("读取完成", "结果")));
        Assert.Same(row, Assert.Single(messages, message => message.IsTool));
        Assert.False(row!.IsToolRunning);
        Assert.Null(row.ToolPhase);
        Assert.Equal("完成", row.ToolStatus);
        Assert.True(row.ToggleToolCommand.CanExecute(null));
        Present(new AgentTextEvent("检查完成。"));
        Assert.Equal(["准备检查。", "读取完成", "检查完成。"], messages.Select(message => message.Tool?.Summary ?? message.Content).ToArray());
        Present(new AgentToolProgressEvent("file.read", "tool-call", ToolExecutionPhase.Executing));
        Assert.Equal(3, messages.Count);
        Assert.False(row.IsToolRunning);
        Assert.Equal("结果", row.Tool!.Details);
    }

    [Theory]
    [InlineData("cancelled", "tool_cancelled", "已取消")]
    [InlineData("failed", "run_failed", "失败")]
    [InlineData("completed", "tool_incomplete", "失败")]
    public void InterruptedRuns_FinishPendingRowsAndRejectLateProgress(string ending, string errorCode, string status)
    {
        var assistant = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        void Present(AgentEvent item) => assistant = WorkspaceAgentEventPresenter.ApplyEvent(item, assistant, messages, _ => { }, _ => { });
        Present(new AgentToolProgressEvent("shell.run", "call", ToolExecutionPhase.AwaitingApproval));
        var row = Assert.Single(messages);
        Present(ending switch
        {
            "cancelled" => new AgentCancelledEvent(),
            "failed" => new AgentFailureEvent("model_failed", "synthetic-secret-not-for-display"),
            _ => new AgentCompletedEvent()
        });
        Assert.False(row.IsToolRunning);
        Assert.Null(row.ToolPhase);
        Assert.Equal(errorCode, row.Tool!.ErrorCode);
        Assert.Equal(status, row.ToolStatus);
        Assert.DoesNotContain("secret", row.ToolSummary, StringComparison.Ordinal);
        Present(new AgentToolProgressEvent("shell.run", "call", ToolExecutionPhase.Executing));
        Assert.Same(row, Assert.Single(messages));
        Assert.False(row.IsToolRunning);
    }

    [Fact]
    public void SubagentDialogue_OpensOnceAndSurvivesPhaseUpdatesWithoutReopeningUserCollapse()
    {
        var assistant = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel>();
        void Present(AgentEvent item) => assistant = WorkspaceAgentEventPresenter.ApplyEvent(item, assistant, messages, _ => { }, _ => { });
        Present(new AgentToolProgressEvent("subagent.run", "child-call", ToolExecutionPhase.Executing));
        var row = Assert.Single(messages);
        Present(new AgentSubagentProgressEvent("child-call", new("子代理", "首个增量", IsPartial: true)));
        Assert.True(row.IsToolExpanded);
        row.ToggleToolCommand.Execute(null);
        Present(new AgentSubagentProgressEvent("child-call", new("子代理", "完整回复")));
        Present(new AgentToolProgressEvent("subagent.run", "child-call", ToolExecutionPhase.Finalizing));
        Assert.False(row.IsToolExpanded);
        Assert.Equal("完整回复", Assert.Single(row.Tool!.Dialogue!).Content);
        Assert.Equal("保存结果", row.ToolStatus);
    }

    [Fact]
    public void InvalidPhases_AreIgnoredAndMetadataNamesRemainBoundedSingleLine()
    {
        var assistant = new ChatMessageViewModel("MossAgent", "");
        var messages = new ObservableCollection<ChatMessageViewModel> { assistant };
        void Present(AgentEvent item) => assistant = WorkspaceAgentEventPresenter.ApplyEvent(item, assistant, messages, _ => { }, _ => { });
        Present(new AgentToolProgressEvent("ignored", "call", (ToolExecutionPhase)99));
        Assert.Same(assistant, Assert.Single(messages));
        Present(new AgentToolProgressEvent("file.read\r\n" + string.Concat(Enumerable.Repeat("😀", 200)), "call", ToolExecutionPhase.Validating));
        var row = Assert.Single(messages);
        Assert.DoesNotContain('\r', row.ToolSummary);
        Assert.DoesNotContain('\n', row.ToolSummary);
        Assert.InRange(row.ToolSummary.Length, 1, 200);
        Assert.DoesNotContain('\uFFFD', row.ToolSummary);
        Assert.False(row.HasToolDetails);
    }
}
