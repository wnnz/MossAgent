using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ChatMessagePresentationTests
{
    private const string Markdown = "# 标题\n\n```csharp\nvar value = 42;\n```\n\n| Name | Value |\n| --- | --- |\n| file | ready |";

    [Theory]
    [InlineData("普通内容")]
    [InlineData(Markdown)]
    public void UserMessage_KeepsRawTextWithoutCreatingHiddenMarkdownBlocks(string content)
    {
        var message = new ChatMessageViewModel("你", content);
        var changes = 0;
        message.Blocks.CollectionChanged += (_, _) => changes++;

        Assert.True(message.IsUserMessage);
        Assert.Equal(content, message.Content);
        Assert.Empty(message.Blocks);
        message.Append("\n下一行");

        Assert.Equal(content + "\n下一行", message.Content);
        Assert.True(message.HasContent);
        Assert.Empty(message.Blocks);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToolMessage_KeepsSummaryDetailsAndDialogueWithoutParsingHiddenBlocks(bool hasDialogue)
    {
        IReadOnlyList<ToolDialogueMessage>? dialogue = hasDialogue ? [new("子代理", Markdown)] : null;
        var payload = new ToolConversationPayload("subagent.run", false, Markdown, "原始详情", dialogue);
        var message = ChatMessageViewModel.CreateTool(payload, "presentation-call", true);

        Assert.True(message.IsTool);
        Assert.False(message.IsAssistantMessage);
        Assert.Equal(Markdown, message.Content);
        Assert.Equal("# 标题", message.ToolSummary);
        Assert.Equal(Markdown, message.ToolSummaryDetails);
        Assert.Empty(message.Blocks);
        message.Content = "新的原始内容";
        message.UpdateTool(payload with { IsSuccess = true, Summary = "完成" }, false);

        Assert.Empty(message.Blocks);
        Assert.Equal("完成", message.ToolSummary);
        Assert.Equal("原始详情", message.Tool!.Details);
        Assert.Equal(hasDialogue, message.Tool.HasDialogue);
        Assert.True(message.ToggleToolCommand.CanExecute(null));
        if (hasDialogue)
        {
            var answer = Assert.Single(message.DialogueEntries);
            Assert.Contains(answer.Blocks, block => block is ChatCodeBlockViewModel);
            Assert.Contains(answer.Blocks, block => block is ChatTableBlockViewModel);
        }
    }

    [Theory]
    [InlineData("MossAgent")]
    [InlineData("系统")]
    public void RegularMessage_KeepsMarkdownAndStreamingNotifications(string role)
    {
        var message = new ChatMessageViewModel(role, Markdown);
        var collection = message.Blocks;
        var changes = 0;
        var properties = new List<string?>();
        collection.CollectionChanged += (_, _) => changes++;
        message.PropertyChanged += (_, args) => properties.Add(args.PropertyName);

        Assert.True(message.IsAssistantMessage);
        Assert.Contains(collection, block => block is ChatCodeBlockViewModel);
        Assert.Contains(collection, block => block is ChatTableBlockViewModel);
        message.Content = "";

        Assert.Same(collection, message.Blocks);
        Assert.Empty(collection);
        Assert.False(message.HasContent);
        Assert.Contains(nameof(message.Content), properties);
        Assert.Contains(nameof(message.HasContent), properties);
        Assert.True(changes > 0);
        message.Append("```json\n{\"ok\":true}\n```");

        var code = Assert.IsType<ChatCodeBlockViewModel>(Assert.Single(collection));
        Assert.Equal("json", code.Language);
        Assert.Equal("{\"ok\":true}", code.Code);
        Assert.True(message.HasContent);
        var priorChanges = changes;
        message.Content = message.Content;
        Assert.Equal(priorChanges, changes);
    }
}
