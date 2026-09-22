using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ChatContentRenderingTests
{
    [Fact]
    public void Parse_CreatesOrderedMarkdownBlocks()
    {
        const string markdown = """
            # 标题

            普通段落

            - 第一项
            > 引用

            ```csharp
            var value = 42;
            ```
            """;

        var blocks = ChatContentParser.Parse(markdown);

        Assert.Collection(
            blocks,
            block => Assert.True(Assert.IsType<ChatTextBlockViewModel>(block).IsHeading),
            block => Assert.True(Assert.IsType<ChatTextBlockViewModel>(block).IsBody),
            block => Assert.True(Assert.IsType<ChatTextBlockViewModel>(block).IsListItem),
            block => Assert.True(Assert.IsType<ChatTextBlockViewModel>(block).IsQuote),
            block =>
            {
                var code = Assert.IsType<ChatCodeBlockViewModel>(block);
                Assert.Equal("csharp", code.Language);
                Assert.Equal("var value = 42;", code.Code);
            });
    }

    [Fact]
    public void ToolMessage_RoundTripsAndTogglesDetails()
    {
        var toolEvent = new AgentToolEvent(
            "file.read", "call-1", ToolResult.Success("读取完成", "content"));
        var serialized = WorkspaceToolMessageSerializer.Serialize(toolEvent);
        var conversation = new ConversationMessage(
            Guid.NewGuid(), Guid.NewGuid(), MessageRole.Tool,
            serialized, DateTimeOffset.UtcNow, "call-1");

        var message = WorkspaceConversationMapper.ToChatMessage(conversation);

        Assert.True(message.IsTool);
        Assert.Equal("file.read", message.Tool?.ToolName);
        Assert.Equal("完成", message.ToolStatus);
        Assert.False(message.IsToolExpanded);

        message.ToggleToolCommand.Execute(null);

        Assert.True(message.IsToolExpanded);
        Assert.Equal("⌄", message.ExpandGlyph);
    }

    [Fact]
    public void Append_ReparsesStreamingCodeFence()
    {
        var message = new ChatMessageViewModel("MossAgent", "```json\n");

        message.Append("{\"ok\":true}\n```");

        var code = Assert.IsType<ChatCodeBlockViewModel>(Assert.Single(message.Blocks));
        Assert.Equal("json", code.Language);
        Assert.Equal("{\"ok\":true}", code.Code);
    }
}
