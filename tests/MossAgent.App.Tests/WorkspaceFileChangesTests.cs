using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFileChangesTests
{
    [Fact]
    public async Task Summary_DeduplicatesWindowsPathsAndObservesFinalToolUpdates()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "src", "file.cs");
        var row = ChatMessageViewModel.CreateTool(new("filesystem.write_file", false, "运行中", null), "change-call", true);
        workspace.Messages.Add(row);
        Assert.False(workspace.FileChanges.HasChanges);
        row.UpdateTool(Payload(path), false);
        workspace.Messages.Add(ChatMessageViewModel.CreateTool(Payload(path.ToUpperInvariant(), 'C')));

        var change = Assert.Single(workspace.FileChanges.Files);
        Assert.Equal(new string('C', 64), change.Change.AfterSha256);
        Assert.Equal("src/file.cs", change.DisplayPath, ignoreCase: true);
        Assert.True(workspace.FileChanges.HasChanges);
        Assert.True(workspace.FileChanges.IsIncomplete);
        Assert.Single(workspace.FileChanges.VisibleFiles);
    }

    [Fact]
    public async Task SwitchingTasks_ClearsSummaryAndDetachesOldRows()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();
        var row = ChatMessageViewModel.CreateTool(Payload(Path.Combine(fixture.Project.PrimaryDirectory, "file.cs")));
        workspace.Messages.Add(row);
        Assert.True(workspace.FileChanges.HasChanges);

        workspace.Catalog.StartNewTask();
        row.UpdateTool(Payload(Path.Combine(fixture.Project.PrimaryDirectory, "late.cs")), false);

        Assert.Empty(workspace.FileChanges.Files);
        Assert.False(workspace.FileChanges.HasChanges);
        Assert.False(workspace.FileChanges.ReviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task PersistedChanges_RestoreWithoutExecutingToolsAndIgnoreLegacyMessages()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        var change = new ToolFileChange(path, null, new string('B', 64), AfterSnapshot:
            new("edit-00000000000000000000000000000000.snapshot", 4, new string('B', 64)));
        var tool = new AgentToolEvent("filesystem.write_file", "saved-change", ToolResult.Success("已写入") with { FileChanges = [change] });
        await fixture.Repository.AppendMessageAsync(new(Guid.NewGuid(), fixture.Task.Id, MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(tool), DateTimeOffset.UtcNow, tool.CallId), fixture.Token);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();

        Assert.Equal(change, Assert.Single(workspace.FileChanges.Files).Change);
        Assert.False(File.Exists(path));
        Assert.DoesNotContain(workspace.Messages, message => message.IsToolRunning);
        workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("old.tool", true, "旧记录", null)));
        Assert.Single(workspace.FileChanges.Files);
    }

    [Fact]
    public async Task ExpansionIncompleteStateAndDisposal_KeepExplicitStates()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();
        for (var index = 0; index < 5; index++)
            workspace.Messages.Add(ChatMessageViewModel.CreateTool(Payload(Path.Combine(fixture.Project.PrimaryDirectory, $"file-{index}.cs"))));
        var summary = workspace.FileChanges;
        Assert.Equal(3, summary.VisibleFiles.Count);
        Assert.True(summary.HasMoreFiles);
        summary.ExpandCommand.Execute(null);
        Assert.Equal(5, summary.VisibleFiles.Count);
        Assert.False(summary.HasMoreFiles);
        workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("subagent.run", false, "失败", null, FileChangesTruncated: true)));
        Assert.True(summary.IsIncomplete);
        summary.Dispose();
        workspace.Messages.Add(ChatMessageViewModel.CreateTool(Payload(Path.Combine(fixture.Project.PrimaryDirectory, "later.cs"))));
        Assert.Equal(5, summary.Files.Count);
        Assert.False(summary.ReviewCommand.CanExecute(null));
        Assert.False(summary.ExpandCommand.CanExecute(null));
    }

    private static ToolConversationPayload Payload(string path, char after = 'B') =>
        new("filesystem.write_file", true, "已写入", null, FileChanges: [new(path, new string('A', 64), new string(after, 64))]);

    [Fact]
    public async Task SummaryPreview_RevalidatesAuthorizedRootsInsteadOfTrustingStoredPaths()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();
        var outside = Path.Combine(Path.GetDirectoryName(fixture.Project.PrimaryDirectory)!,
            Path.GetFileName(fixture.Project.PrimaryDirectory) + "-outside-secret.txt");
        await File.WriteAllTextAsync(outside, "synthetic-outside-content", fixture.Token);
        try
        {
            workspace.Messages.Add(ChatMessageViewModel.CreateTool(Payload(outside)));
            await workspace.FileChanges.OpenFileCommand.ExecuteAsync(Assert.Single(workspace.FileChanges.Files).Path);

            Assert.NotNull(workspace.FilePreview.Active);
            Assert.False(workspace.FilePreview.Active.IsReady);
            Assert.False(workspace.FilePreview.Active.IsLoading);
            Assert.DoesNotContain("synthetic-outside-content", workspace.FilePreview.Active.Text);
        }
        finally { File.Delete(outside); }
    }
}
