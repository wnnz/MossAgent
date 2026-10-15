using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFileChangesRefreshTests
{
    [Fact]
    public async Task UnrelatedMessagesAndToolProgress_DoNotRepublishSummary()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        messages.Add(ChatMessageViewModel.CreateTool(Payload(path)));
        var summary = fixture.Workspace.FileChanges;
        var visible = summary.VisibleFiles;
        var file = Assert.Single(summary.Files);
        var notifications = 0;
        summary.PropertyChanged += (_, _) => notifications++;
        summary.Files.CollectionChanged += (_, _) => notifications++;
        var assistant = new ChatMessageViewModel("MossAgent", "回答");
        messages.Add(assistant);
        assistant.Append("继续");
        messages.Move(messages.Count - 1, 0);
        messages.Remove(assistant);
        var progress = ChatMessageViewModel.CreateTool(new("subagent.run", false, "运行中", null));
        messages.Add(progress);
        progress.UpdateTool(new("subagent.run", false, "新进度", null), true);
        messages[messages.IndexOf(progress)] = new("你", "问题");

        Assert.Equal(0, notifications);
        Assert.Same(file, Assert.Single(summary.Files));
        Assert.Same(visible, summary.VisibleFiles);
        Assert.Same(visible, summary.VisibleFiles);
    }

    [Fact]
    public async Task EquivalentFileRecords_RetainRowsAndCollapsedSnapshot()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        var row = ChatMessageViewModel.CreateTool(Payload(path));
        messages.Add(row);
        var summary = fixture.Workspace.FileChanges;
        var visible = summary.VisibleFiles;
        var file = Assert.Single(summary.Files);
        var notifications = 0;
        summary.PropertyChanged += (_, _) => notifications++;
        summary.Files.CollectionChanged += (_, _) => notifications++;

        row.UpdateTool(Payload(path) with { Summary = "不同摘要" }, false);
        var equivalent = ChatMessageViewModel.CreateTool(Payload(path));
        messages.Add(equivalent);
        messages.Remove(equivalent);

        Assert.Equal(0, notifications);
        Assert.Same(file, Assert.Single(summary.Files));
        Assert.Same(visible, summary.VisibleFiles);
    }

    [Fact]
    public async Task ChangedRecord_ReplacesOnlyAffectedRowAndPreservesPreviousSnapshot()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var root = fixture.Project.PrimaryDirectory;
        var first = ChatMessageViewModel.CreateTool(Payload(Path.Combine(root, "first.cs")));
        var second = ChatMessageViewModel.CreateTool(Payload(Path.Combine(root, "second.cs")));
        messages.Add(first);
        messages.Add(second);
        var summary = fixture.Workspace.FileChanges;
        var before = summary.VisibleFiles;
        var changes = new List<NotifyCollectionChangedAction>();
        summary.Files.CollectionChanged += (_, args) => changes.Add(args.Action);

        second.UpdateTool(Payload(Path.Combine(root, "second.cs"), 'C'), false);

        Assert.Equal([NotifyCollectionChangedAction.Replace], changes);
        Assert.Same(before[0], summary.VisibleFiles[0]);
        Assert.NotSame(before[1], summary.VisibleFiles[1]);
        Assert.Equal(new string('B', 64), before[1].Change.AfterSha256);
        Assert.Equal(new string('C', 64), summary.VisibleFiles[1].Change.AfterSha256);
        Assert.Same(summary.VisibleFiles, summary.VisibleFiles);
    }

    [Fact]
    public async Task DuplicateMessageReferences_RemainObservedUntilLastRemoval()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        var row = ChatMessageViewModel.CreateTool(Payload(path));
        messages.Add(row);
        messages.Add(row);
        messages.Remove(row);
        var summary = fixture.Workspace.FileChanges;
        var changes = 0;
        summary.Files.CollectionChanged += (_, _) => changes++;

        row.UpdateTool(Payload(path, 'C'), false);

        Assert.Equal(1, changes);
        Assert.Equal(new string('C', 64), Assert.Single(summary.Files).Change.AfterSha256);
        messages.Remove(row);
        Assert.Empty(summary.Files);
        changes = 0;
        row.UpdateTool(Payload(path, 'D'), false);
        Assert.Equal(0, changes);
        Assert.Empty(summary.Files);
    }

    [Fact]
    public async Task ReorderedToolMessages_KeepLastWriteWinsInConversationOrder()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        var first = ChatMessageViewModel.CreateTool(Payload(path));
        var second = ChatMessageViewModel.CreateTool(Payload(path, 'C'));
        messages.Add(first);
        messages.Add(second);
        var summary = fixture.Workspace.FileChanges;
        Assert.Equal(new string('C', 64), Assert.Single(summary.Files).Change.AfterSha256);

        messages.Move(messages.IndexOf(first), messages.Count - 1);

        Assert.Equal(new string('B', 64), Assert.Single(summary.Files).Change.AfterSha256);
        messages.Remove(first);
        Assert.Equal(new string('C', 64), Assert.Single(summary.Files).Change.AfterSha256);
    }

    [Fact]
    public async Task ResetAndReplacedRows_DetachOldSubscriptionsAndObserveNewToolResults()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        var old = ChatMessageViewModel.CreateTool(Payload(path));
        messages.Add(old);
        var summary = fixture.Workspace.FileChanges;
        messages.Clear();
        old.UpdateTool(Payload(path, 'C'), false);
        Assert.False(summary.HasChanges);
        var next = new ChatMessageViewModel("MossAgent", "等待工具");
        messages.Add(next);
        next.UpdateTool(Payload(path), false);
        Assert.Single(summary.Files);
        var replacement = ChatMessageViewModel.CreateTool(Payload(path, 'D'));
        messages[messages.IndexOf(next)] = replacement;
        next.UpdateTool(Payload(path, 'C'), false);

        Assert.Equal(new string('D', 64), Assert.Single(summary.Files).Change.AfterSha256);
    }

    [Fact]
    public async Task RemovedChangeMetadataAndTruncation_RefreshWithoutStaleRows()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var messages = fixture.Workspace.Messages;
        var row = ChatMessageViewModel.CreateTool(Payload(Path.Combine(fixture.Project.PrimaryDirectory, "file.cs")));
        messages.Add(row);
        var summary = fixture.Workspace.FileChanges;

        row.UpdateTool(new("subagent.run", false, "记录不完整", null, FileChangesTruncated: true), false);

        Assert.Empty(summary.Files);
        Assert.True(summary.HasChanges);
        Assert.True(summary.IsIncomplete);
        row.UpdateTool(new("subagent.run", true, "无变更", null), false);
        Assert.False(summary.HasChanges);
        Assert.False(summary.IsIncomplete);
        Assert.Empty(summary.VisibleFiles);
    }

    private static ToolConversationPayload Payload(string path, char after = 'B') =>
        new("filesystem.write_file", true, "已写入", null, FileChanges: [new(path, new string('A', 64), new string(after, 64))]);
}
