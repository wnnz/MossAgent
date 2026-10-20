using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerNavigationTests
{
    [Fact]
    public async Task ProjectAndProjectlessNewConversationsKeepSeparateDrafts()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var project = workspace.Catalog.SelectedProject!;
        workspace.ComposerText = "项目内尚未发送的内容";

        workspace.Catalog.NewChatCommand.Execute(null);

        Assert.Null(workspace.Catalog.SelectedProject);
        Assert.Empty(workspace.ComposerText);
        workspace.ComposerText = "无项目聊天的独立草稿";
        workspace.Catalog.SelectedProject = project;
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedProject?.Id == project.Id);
        Assert.Equal("项目内尚未发送的内容", workspace.ComposerText);
        workspace.Catalog.NewChatCommand.Execute(null);
        Assert.Equal("无项目聊天的独立草稿", workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
    }

    [Fact]
    public async Task ExistingConversationsDoNotBorrowEachOthersDrafts()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var now = DateTimeOffset.UtcNow;
        var first = new AgentTask(Guid.NewGuid(), workspace.Catalog.SelectedProject!.Id, "first",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        var second = first with { Id = Guid.NewGuid(), Title = "second" };
        await fixture.Tasks.SaveTaskAsync(first, fixture.Token);
        await fixture.Tasks.SaveTaskAsync(second, fixture.Token);
        workspace.Catalog.UpsertTask(first);
        workspace.ComposerText = "第一段对话的草稿";

        workspace.Catalog.UpsertTask(second);

        Assert.Empty(workspace.ComposerText);
        workspace.ComposerText = "第二段对话的草稿";
        workspace.Catalog.SelectedTask = first;
        Assert.Equal("第一段对话的草稿", workspace.ComposerText);
        workspace.Catalog.SelectedTask = second;
        Assert.Equal("第二段对话的草稿", workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
    }
}
