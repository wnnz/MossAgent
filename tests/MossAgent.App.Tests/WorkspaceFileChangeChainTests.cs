using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFileChangeChainTests
{
    [Fact]
    public async Task ContinuousEdits_PreserveTheFirstBeforeSnapshotAndLastAfterSnapshot()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        var first = Change(path, 'A', 'B', '1', '2');
        var last = Change(path, 'B', 'C', '3', '4');
        Add(fixture.Workspace, first);
        Add(fixture.Workspace, last);
        var combined = Assert.Single(fixture.Workspace.FileChanges.Files).Change;
        Assert.Equal(first.BeforeSha256, combined.BeforeSha256);
        Assert.Equal(first.BeforeSnapshot, combined.BeforeSnapshot);
        Assert.Equal(last.AfterSnapshot, combined.AfterSnapshot);
        Assert.False(fixture.Workspace.FileChanges.IsIncomplete);
    }

    [Fact]
    public async Task NetNoChange_RemovesTheFileWithoutClaimingAnIncompleteHistory()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        Add(fixture.Workspace, Change(path, 'A', 'B', '1', '2'));
        Add(fixture.Workspace, Change(path, 'B', 'A', '3', '4'));
        Assert.Empty(fixture.Workspace.FileChanges.Files);
        Assert.False(fixture.Workspace.FileChanges.HasChanges);
        Assert.False(fixture.Workspace.FileChanges.IsIncomplete);
        Assert.False(fixture.Workspace.FileChanges.ReviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task DiscontinuousEdits_DoNotBlendExternalVersionsIntoTaskHistory()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "file.cs");
        Add(fixture.Workspace, Change(path, 'A', 'B', '1', '2'));
        var last = Change(path, 'C', 'D', '3', '4');
        Add(fixture.Workspace, last);
        Assert.Equal(last, Assert.Single(fixture.Workspace.FileChanges.Files).Change);
        Assert.True(fixture.Workspace.FileChanges.IsIncomplete);
        fixture.Workspace.FileChanges.Dispose();
        Assert.False(fixture.Workspace.FileChanges.OpenFileCommand.CanExecute(path));
    }

    private static ToolFileChange Change(string path, char before, char after, char beforeName, char afterName) =>
        new(path, new string(before, 64), new string(after, 64),
            new($"edit-{new string(beforeName, 32)}.snapshot", 3, new string(before, 64)),
            new($"edit-{new string(afterName, 32)}.snapshot", 3, new string(after, 64)));

    [Fact]
    public async Task CreatedThenRemovedFile_IsNetZeroAndCanBeCreatedAgain()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await fixture.Workspace.LoadAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "new.cs");
        var created = new ToolFileChange(path, null, new string('A', 64));
        var removed = new ToolFileChange(path, new string('A', 64), null, AfterDeleted: true);
        Add(fixture.Workspace, created);
        Add(fixture.Workspace, removed);
        Assert.Empty(fixture.Workspace.FileChanges.Files);
        Assert.False(fixture.Workspace.FileChanges.IsIncomplete);
        Add(fixture.Workspace, new(path, null, new string('B', 64)));
        var latest = Assert.Single(fixture.Workspace.FileChanges.Files).Change;
        Assert.Null(latest.BeforeSha256);
        Assert.Equal(new string('B', 64), latest.AfterSha256);
        Assert.False(fixture.Workspace.FileChanges.IsIncomplete);
    }

    private static void Add(WorkspaceViewModel workspace, ToolFileChange change) => workspace.Messages.Add(
        ChatMessageViewModel.CreateTool(new("filesystem.write_file", true, "已写入", null, FileChanges: [change])));
}
