using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceToolProgressOptimizationTests(ITestOutputHelper output)
{
    [Fact]
    public void LateProgress_DoesNotAllocateLookupClosuresOrMutateCompletedRows()
    {
        var messages = new ObservableCollection<ChatMessageViewModel>();
        for (var index = 0; index < 1_000; index++) messages.Add(new("你", string.Empty));
        var completed = ChatMessageViewModel.CreateTool(new("file.read", true, "完成", "原始结果"), "call");
        messages.Add(completed);
        var payload = completed.Tool;
        var progress = new AgentToolProgressEvent("FILE.READ", "call", ToolExecutionPhase.Executing);
        for (var index = 0; index < 128; index++) WorkspaceToolProgressPresenter.Apply(messages, progress);
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++) WorkspaceToolProgressPresenter.Apply(messages, progress);
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - start) / 1_000;
        output.WriteLine($"Completed row lookup allocation: {bytes} bytes/operation");
        Assert.Same(payload, completed.Tool);
        Assert.False(completed.IsToolRunning);
        Assert.Null(completed.ToolPhase);
        Assert.Equal(1_001, messages.Count);
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void Progress_RetainsFirstOccurrenceAndExactCallIdentity()
    {
        var otherTool = ChatMessageViewModel.CreateTool(new("other", false, "保留", null), "call", true);
        var completed = ChatMessageViewModel.CreateTool(new("FILE.READ", true, "已完成", "结果"), "call");
        var running = ChatMessageViewModel.CreateTool(new("file.read", false, "开始", null), "call", true);
        var differentCase = ChatMessageViewModel.CreateTool(new("file.read", false, "保留", null), "CALL", true);
        var messages = new ObservableCollection<ChatMessageViewModel> { otherTool, differentCase, completed, running };
        var progress = new AgentToolProgressEvent("file.read", "call", ToolExecutionPhase.Executing);

        WorkspaceToolProgressPresenter.Apply(messages, progress);
        Assert.Null(running.ToolPhase);
        Assert.Equal("已完成", completed.ToolSummary);
        messages.Remove(completed);
        WorkspaceToolProgressPresenter.Apply(messages, progress);

        Assert.Equal(ToolExecutionPhase.Executing, running.ToolPhase);
        Assert.Equal("保留", otherTool.ToolSummary);
        Assert.Equal("保留", differentCase.ToolSummary);
        Assert.Equal(3, messages.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalAndChildEvents_UseTheFirstRunningCallWithoutResurrectingCompletedRows(bool child)
    {
        var completed = ChatMessageViewModel.CreateTool(new("shell.run", true, "已完成", null), "call");
        var differentCase = ChatMessageViewModel.CreateTool(new("shell.run", false, "保留", null), "CALL", true);
        var first = ChatMessageViewModel.CreateTool(new("subagent.run", false, "开始", null), "call", true);
        var second = ChatMessageViewModel.CreateTool(new("subagent.run", false, "稍后", null), "call", true);
        var messages = new ObservableCollection<ChatMessageViewModel> { completed, differentCase, first, second };
        AgentEvent progress = child
            ? new AgentSubagentProgressEvent("call", new("子代理", "增量"))
            : new AgentToolEvent("shell.run", "call", ToolResult.Success("工具结果", "正文"));

        WorkspaceAgentEventPresenter.ApplyEvent(progress, new("MossAgent", string.Empty), messages, _ => { }, _ => { });

        Assert.Equal(4, messages.Count);
        Assert.Equal("已完成", completed.ToolSummary);
        Assert.Equal("保留", differentCase.ToolSummary);
        Assert.Equal("稍后", second.ToolSummary);
        Assert.True(second.IsToolRunning);
        if (child)
        {
            Assert.Equal("增量", Assert.Single(first.Tool!.Dialogue!).Content);
            Assert.True(first.IsToolRunning);
        }
        else
        {
            Assert.Equal("工具结果", first.ToolSummary);
            Assert.False(first.IsToolRunning);
        }
    }

    [Fact]
    public void Progress_RemainsOwnedByEachMessageCollection()
    {
        var first = new ObservableCollection<ChatMessageViewModel>();
        var second = new ObservableCollection<ChatMessageViewModel>();
        var progress = new AgentToolProgressEvent("file.read", "shared-call", ToolExecutionPhase.Validating);
        WorkspaceToolProgressPresenter.Apply(first, progress);
        WorkspaceToolProgressPresenter.Apply(second, progress);
        var firstRow = Assert.Single(first);
        var secondRow = Assert.Single(second);
        WorkspaceToolProgressPresenter.Apply(first, progress with { Phase = ToolExecutionPhase.Executing });
        Assert.NotSame(firstRow, secondRow);
        Assert.Equal(ToolExecutionPhase.Executing, firstRow.ToolPhase);
        Assert.Equal(ToolExecutionPhase.Validating, secondRow.ToolPhase);
    }
}
