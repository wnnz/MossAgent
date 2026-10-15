using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
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
    public void Parse_PreservesNestedListIndentation()
    {
        const string markdown = "- root\n  - child\n    1. grandchild";

        var listItems = ChatContentParser.Parse(markdown)
            .OfType<ChatTextBlockViewModel>().ToArray();

        Assert.Equal(new[] { 0, 1, 2 }, listItems.Select(static item => item.IndentLevel));
        Assert.Equal("1.", listItems[2].Prefix);
    }

    [Fact]
    public void Parse_ParsesPipeTablesAndEscapedPipes()
    {
        const string markdown = "| Name | Value |\n| :--- | ---: |\n| branch | feature\\|markdown |";

        var table = Assert.IsType<ChatTableBlockViewModel>(Assert.Single(ChatContentParser.Parse(markdown)));

        Assert.Equal(new[] { "Name", "Value" }, table.Headers);
        Assert.Equal("feature|markdown", Assert.Single(table.Rows)[1]);
    }

    [Fact]
    public void Parse_DoesNotTreatPipeTablesInsideCodeFencesAsTables()
    {
        const string markdown = "```md\n| Name | Value |\n| --- | --- |\n```";

        var code = Assert.IsType<ChatCodeBlockViewModel>(
            Assert.Single(ChatContentParser.Parse(markdown)));

        Assert.Contains("| Name | Value |", code.Code);
    }

    [Fact]
    public void MarkdownTextBlock_RendersInlineStylesAndKeepsTextSelectable()
    {
        var textBlock = new MarkdownSelectableTextBlock
        {
            Markdown = "**bold** *italic* ~~removed~~ `code` [link](https://example.com)"
        };
        var inlines = Assert.IsType<InlineCollection>(textBlock.Inlines);

        Assert.Contains(inlines.OfType<Span>(),
            static span => span.FontWeight == FontWeight.Bold);
        Assert.Contains(inlines.OfType<Span>(),
            static span => span.FontStyle == FontStyle.Italic);
        Assert.Contains(inlines.OfType<Span>(),
            static span => span.TextDecorations == TextDecorations.Strikethrough);
        Assert.Contains(inlines.OfType<Span>(),
            static span => span.TextDecorations == TextDecorations.Underline);
        var inlineCode = Assert.Single(inlines.OfType<Run>(), static run => run.Text == "code");
        Assert.StartsWith("Consolas", inlineCode.FontFamily?.Name);

        textBlock.CodeFontFamily = new FontFamily("Cascadia Code");

        Assert.Equal("Cascadia Code", inlineCode.FontFamily?.Name);
    }

    [Fact]
    public void CodeSyntaxHighlighter_StylesKnownTokensAndPreservesSourceText()
    {
        const string code = "public class Widget { const int Count = 42; string title = \"Moss\"; // ready";

        var tokens = CodeSyntaxHighlighter.Tokenize("csharp", code);

        Assert.Equal(code, string.Concat(tokens.Select(static token => token.Text)));
        Assert.Contains(tokens, static token =>
            token.Text == "public" && token.ResourceKey == "CodeKeywordBrush");
        Assert.Contains(tokens, static token =>
            token.Text == "Widget" && token.ResourceKey == "CodeTypeBrush");
        Assert.Contains(tokens, static token =>
            token.Text == "42" && token.ResourceKey == "CodeNumberBrush");
        Assert.Contains(tokens, static token =>
            token.Text == "\"Moss\"" && token.ResourceKey == "CodeStringBrush");
        Assert.Contains(tokens, static token =>
            token.Text == "// ready" && token.ResourceKey == "CodeCommentBrush");
    }

    /// <summary>
    /// 验证三种消息角色映射到各自的展示区域，不让工具输出误入用户气泡。
    /// </summary>
    [Fact]
    public void MessageLayout_SeparatesUserBubbleFromMarkdownAndToolContent()
    {
        var user = new ChatMessageViewModel("你", "请检查变更");
        var assistant = new ChatMessageViewModel("MossAgent", "检查完成");
        var tool = ChatMessageViewModel.CreateTool(new ToolConversationPayload(
            "file.read", true, "读取文件", "详情"));

        Assert.True(user.IsUserMessage);
        Assert.False(user.IsAssistantMessage);
        Assert.True(assistant.IsAssistantMessage);
        Assert.False(assistant.IsUserMessage);
        Assert.False(tool.IsUserMessage);
        Assert.False(tool.IsAssistantMessage);
    }

    [Fact]
    public void UserMessage_CopiesAttachmentMetadataForRendering()
    {
        var source = new List<ChatMessageAttachmentViewModel>
        {
            new("src/Program.cs", "2.4 KiB")
        };

        var message = new ChatMessageViewModel("你", "请阅读这个文件", attachments: source);
        source.Clear();

        Assert.True(message.HasAttachments);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("src/Program.cs", attachment.DisplayPath);
        Assert.Equal("2.4 KiB", attachment.SizeLabel);
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
        Assert.Equal("call-1", message.ToolCallId);
        Assert.Equal("完成", message.ToolStatus);
        Assert.False(message.IsToolExpanded);

        message.ToggleToolCommand.Execute(null);

        Assert.True(message.IsToolExpanded);
        message.ToggleToolCommand.Execute(null);
        Assert.False(message.IsToolExpanded);
    }

    [Fact]
    public void ToolMessage_PersistsAndExposesSubagentDialogue()
    {
        var toolEvent = new AgentToolEvent(
            "subagent.run", "call-2",
            ToolResult.Success("子代理已完成", "报告", [
                new ToolDialogueMessage("任务", "检查改动"),
                new ToolDialogueMessage("状态", "执行工具 · filesystem.read_file"),
                new ToolDialogueMessage("工具 · filesystem.read_file", "读取完成"),
                new ToolDialogueMessage("子代理", "发现一个问题")
            ]));
        var message = WorkspaceConversationMapper.ToChatMessage(new ConversationMessage(
            Guid.NewGuid(), Guid.NewGuid(), MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(toolEvent), DateTimeOffset.UtcNow, "call-2"));

        Assert.True(message.HasToolDetails);
        Assert.True(message.IsSubagentTool);
        Assert.Equal("call-2", message.ToolCallId);
        Assert.True(message.IsToolExpanded);
        Assert.True(message.Tool!.HasDialogue);
        Assert.False(message.Tool.HasDetails);
        Assert.Equal("检查改动", message.Tool.Dialogue![0].Content);
        Assert.Equal("读取完成", message.Tool.Dialogue[2].Content);
        Assert.Equal("发现一个问题", message.Tool.Dialogue[3].Content);
    }

    [Fact]
    public void SubagentMessage_UpdatesRunningStatusWithoutDuplicatingCard()
    {
        var message = ChatMessageViewModel.CreateTool(
            new ToolConversationPayload("subagent.run", false, "子代理运行中",
                null, [new ToolDialogueMessage("状态", "模型处理中")]),
            "call-3", true);

        Assert.Equal("call-3", message.ToolCallId);
        Assert.Equal("运行中", message.ToolStatus);
        Assert.True(message.IsToolExpanded);

        message.UpdateTool(new ToolConversationPayload(
            "subagent.run", true, "子代理已完成", "报告",
            [new ToolDialogueMessage("子代理", "发现问题")]), false);

        Assert.Equal("完成", message.ToolStatus);
        Assert.Equal("子代理已完成", message.Tool!.Summary);
        Assert.Equal("发现问题", Assert.Single(message.Tool.Dialogue!).Content);
    }

    [Fact]
    public void Append_ReparsesStreamingCodeFence()
    {
        var message = new ChatMessageViewModel("MossAgent", string.Empty);
        Assert.False(message.HasContent);

        message.Append("```json\n{\"ok\":true}\n```");

        Assert.True(message.HasContent);
        var code = Assert.IsType<ChatCodeBlockViewModel>(Assert.Single(message.Blocks));
        Assert.Equal("json", code.Language);
        Assert.Equal("{\"ok\":true}", code.Code);
    }
}
