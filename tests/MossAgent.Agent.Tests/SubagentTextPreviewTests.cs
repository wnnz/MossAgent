using System.Text;
using MossAgent.Agent;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class SubagentTextPreviewTests
{
    [Fact]
    public void Report_PublishesFirstChunkAndFlushesTheLatestText()
    {
        var messages = new List<ToolDialogueMessage>();
        var preview = new SubagentTextPreview(messages.Add);
        var text = new StringBuilder("正在");
        preview.Report(text);
        Assert.Equal("正在", Assert.Single(messages).Content);
        text.Append("审查");
        preview.Report(text);
        Assert.Single(messages);
        preview.Report(text, force: true);
        Assert.Equal("正在审查", messages[^1].Content);
        Assert.All(messages, message => Assert.True(message.IsPartial));
    }

    [Fact]
    public void Report_BoundsPreviewSizeAndUpdateCount()
    {
        var messages = new List<ToolDialogueMessage>();
        var preview = new SubagentTextPreview(messages.Add);
        var text = new StringBuilder();
        for (var index = 0; index < 12_000; index++)
        {
            text.Append('a');
            preview.Report(text);
        }
        preview.Report(text, force: true);

        Assert.InRange(messages.Count, 1, 19);
        Assert.All(messages, message => Assert.True(message.Content.Length <= 1_024 + "\n[预览已截断]".Length));
        Assert.EndsWith("[预览已截断]", messages[^1].Content);
    }

    [Fact]
    public void Report_DoesNotPublishSplitSurrogatePairs()
    {
        var messages = new List<ToolDialogueMessage>();
        var preview = new SubagentTextPreview(messages.Add);
        var text = new StringBuilder("\uD83D");
        preview.Report(text);
        Assert.Empty(messages);
        text.Append('\uDE00');
        preview.Report(text);
        Assert.Equal("😀", Assert.Single(messages).Content);

        text.Clear().Append('a', 1_023).Append("😀tail");
        preview.Report(text, force: true);
        Assert.DoesNotContain(messages[^1].Content, char.IsSurrogate);
        Assert.DoesNotContain(SubagentTextPreview.CreateMessage("子代理", text.ToString()).Content,
            char.IsSurrogate);
    }
}
