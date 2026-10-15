using System.Xml.Linq;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentDialoguePresentationTests
{
    [Fact]
    public void DialogueEntries_ClassifyEventsWithoutChangingPersistedContent()
    {
        var source = new[]
        {
            new ToolDialogueMessage("任务", "检查改动"),
            new ToolDialogueMessage("状态", "执行工具 · filesystem.read_file"),
            new ToolDialogueMessage("工具 · filesystem.read_file", "读取完成\n第二行"),
            new ToolDialogueMessage("子代理", "发现问题"),
            new ToolDialogueMessage("其他事件", "保留原文")
        };
        var payload = new ToolConversationPayload("subagent.run", true, "完成", null, source);
        var entries = ChatMessageViewModel.CreateTool(payload).DialogueEntries;

        Assert.True(entries[0].IsTask);
        Assert.True(entries[1].IsStatus);
        Assert.False(entries[1].ShowLabel);
        Assert.True(entries[2].IsTool);
        Assert.Equal("filesystem.read_file", entries[2].DisplayRole);
        Assert.True(entries[3].IsAnswer);
        Assert.True(entries[4].IsOther);
        Assert.Equal(source.Select(message => message.Content), entries.Select(entry => entry.Content));
        Assert.Equal("读取完成\n第二行", entries[2].Content);
    }

    [Fact]
    public void RunningCard_UsesUpdatedDialogueEntriesAfterCompletion()
    {
        var message = ChatMessageViewModel.CreateTool(new ToolConversationPayload(
            "subagent.run", false, "运行中", null,
            [new ToolDialogueMessage("状态", "模型处理中")]), "call-1", true);

        Assert.True(Assert.Single(message.DialogueEntries).IsStatus);
        message.UpdateTool(new ToolConversationPayload("subagent.run", true, "完成", null,
            [new ToolDialogueMessage("子代理", "审查完成")]), false);

        Assert.False(message.IsToolRunning);
        Assert.True(Assert.Single(message.DialogueEntries).IsAnswer);
        Assert.Equal("审查完成", message.DialogueEntries[0].Content);
    }

    [Fact]
    public void Conversation_UsesReusableSubagentEventView()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var chat = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceMessageView.axaml"));
        var dialogue = XDocument.Load(Path.Combine(appPath, "Views", "SubagentDialogueView.axaml"));
        var view = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "SubagentDialogueView");
        var items = Assert.Single(dialogue.Descendants(), element => element.Name.LocalName == "AppItemsControl");

        Assert.Null(view.Attribute("DataContext"));
        Assert.Equal("{Binding Tool.HasDialogue}", view.Attribute("IsVisible")?.Value);
        Assert.Equal("{Binding DialogueEntries}", items.Attribute("ItemsSource")?.Value);
        Assert.Contains(dialogue.Descendants(), element => element.Attribute("Classes.tool")?.Value == "{Binding IsTool}");
        Assert.Contains(dialogue.Descendants(), element => element.Attribute("Classes.answer")?.Value == "{Binding IsAnswer}");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
