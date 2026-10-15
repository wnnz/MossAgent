using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentDialogueProgressTests
{
    [Fact]
    public void Apply_ReplacesOnlyTheCurrentPartialReply()
    {
        var initial = new ToolDialogueMessage("任务", "检查改动");
        var dialogue = SubagentDialogueProgress.Apply([initial], new("子代理", "正在", IsPartial: true));
        dialogue = SubagentDialogueProgress.Apply(dialogue, new("子代理", "正在审查", IsPartial: true));
        Assert.Equal(2, dialogue.Count);
        Assert.Same(initial, dialogue[0]);
        dialogue = SubagentDialogueProgress.Apply(dialogue, new("子代理", "正在审查"));
        Assert.Equal(2, dialogue.Count);
        Assert.False(dialogue[^1].IsPartial);
        Assert.Equal("正在审查", dialogue[^1].Content);
    }

    [Fact]
    public void Apply_PreservesEarlierTurnsAndToolMessages()
    {
        var completed = new ToolDialogueMessage("子代理", "第一轮");
        var tool = new ToolDialogueMessage("工具 · filesystem.read_file", "已读取");
        var dialogue = SubagentDialogueProgress.Apply([completed, tool], new("子代理", "第二轮", IsPartial: true));
        Assert.Equal([completed, tool], dialogue.Take(2));
        Assert.Equal(3, dialogue.Count);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Apply_OnlyReplacesATrailingPartialFromTheSameRole(
        bool previousPartial, bool incomingPartial, bool sameRole)
    {
        var initial = new ToolDialogueMessage("任务", "检查改动", IsPartial: true);
        var trailing = new ToolDialogueMessage("子代理", "之前", previousPartial);
        var incoming = new ToolDialogueMessage(sameRole ? "子代理" : "工具 · read_file", "之后", incomingPartial);
        ToolDialogueMessage[] previous = [initial, trailing];

        var result = SubagentDialogueProgress.Apply(previous, incoming);

        Assert.NotSame(previous, result);
        Assert.Equal(previousPartial && sameRole ? 2 : 3, result.Count);
        Assert.Same(initial, result[0]);
        Assert.Same(incoming, result[^1]);
        if (!previousPartial || !sameRole) Assert.Same(trailing, result[1]);
        Assert.Equal([initial, trailing], previous);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_NullOrEmptyHistoryStartsWithTheIncomingMessage(bool nullHistory)
    {
        IReadOnlyList<ToolDialogueMessage>? previous = nullHistory ? null : [];
        var incoming = new ToolDialogueMessage("子代理", "开始", IsPartial: true);

        var result = SubagentDialogueProgress.Apply(previous, incoming);

        Assert.Same(incoming, Assert.Single(result));
        Assert.NotSame(previous, result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_IsolatesBranchesAndSupportsReadOnlyHistory(bool readOnly)
    {
        var initial = new ToolDialogueMessage("任务", "检查改动");
        IReadOnlyList<ToolDialogueMessage> previous = readOnly
            ? Array.AsReadOnly(new[] { initial }) : new List<ToolDialogueMessage> { initial };
        var firstReply = new ToolDialogueMessage("子代理", "第一支", IsPartial: true);
        var secondReply = new ToolDialogueMessage("子代理", "第二支", IsPartial: true);
        var completed = new ToolDialogueMessage("子代理", "第一支完成");

        var first = SubagentDialogueProgress.Apply(previous, firstReply);
        var second = SubagentDialogueProgress.Apply(previous, secondReply);
        var final = SubagentDialogueProgress.Apply(first, completed);

        Assert.Same(initial, Assert.Single(previous));
        Assert.Equal([initial, firstReply], first);
        Assert.Equal([initial, secondReply], second);
        Assert.Equal([initial, completed], final);
        Assert.NotSame(first, final);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ExistingPersistedDialogue_DoesNotBecomePartial()
    {
        var message = JsonSerializer.Deserialize<ToolDialogueMessage>("""{"Role":"子代理","Content":"已完成"}""");
        Assert.NotNull(message);
        Assert.False(message.IsPartial);
        Assert.Equal("已完成", message.Content);
    }
}
