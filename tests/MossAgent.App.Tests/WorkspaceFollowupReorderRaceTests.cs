using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupReorderRaceTests
{
    [Fact]
    public async Task StaleSnapshotReorderReportsConflictAndPreservesTheWrittenOrder()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        foreach (var text in new[] { "队列第一条", "队列第二条", "队列第三条" })
        {
            workspace.ComposerText = text;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        var taskId = workspace.Catalog.SelectedTask!.Id;
        var persisted = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.True(await fixture.Followups.SaveAsync(persisted with
        { Items = [persisted.Items[1], persisted.Items[2], persisted.Items[0]] }, [], fixture.Token));
        await workspace.Queue.Items[2].MoveUpCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => workspace.Queue.HasNotice);
        Assert.Contains("未覆盖已有内容", workspace.Queue.Notice);
        var current = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.Equal(new[] { "队列第二条", "队列第三条", "队列第一条" }, current.Items.Select(item => item.Prompt).ToArray());
        Assert.Equal(new[] { "队列第一条", "队列第二条", "队列第三条" }, workspace.Queue.Items.Select(item => item.Prompt).ToArray());
        await workspace.Queue.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "队列第二条", "队列第三条", "队列第一条" }, workspace.Queue.Items.Select(item => item.Prompt).ToArray());
        Assert.DoesNotContain("未覆盖已有内容", workspace.Queue.Notice);
    }

    [Fact]
    public async Task StaleSnapshotReorderCannotResurrectAClaimedHead()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        foreach (var text in new[] { "已被认领的首项", "保留的次项" })
        {
            workspace.ComposerText = text;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        var taskId = workspace.Catalog.SelectedTask!.Id;
        var persisted = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.True(await fixture.Followups.SaveAsync(persisted with { Items = persisted.Items.Skip(1).ToArray() }, [], fixture.Token));
        await workspace.Queue.Items[1].MoveUpCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => workspace.Queue.HasNotice);
        Assert.Contains("未覆盖已有内容", workspace.Queue.Notice);
        var current = await fixture.Followups.GetAsync(taskId, fixture.Token);
        var survivor = Assert.Single(current.Items);
        Assert.Equal("保留的次项", survivor.Prompt);
    }

    [Fact]
    public async Task StaleSnapshotRemovalReportsConflictAndKeepsTheEntry()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        foreach (var text in new[] { "被外部重排的甲", "被外部重排的乙" })
        {
            workspace.ComposerText = text;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        var taskId = workspace.Catalog.SelectedTask!.Id;
        var persisted = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.True(await fixture.Followups.SaveAsync(persisted with
        { Items = [persisted.Items[1], persisted.Items[0]] }, [], fixture.Token));
        await workspace.Queue.Items[0].DeleteCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => workspace.Queue.HasNotice);
        Assert.Contains("未覆盖已有内容", workspace.Queue.Notice);
        var current = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.Equal(2, current.Items.Count);
        Assert.Equal(new[] { "被外部重排的乙", "被外部重排的甲" }, current.Items.Select(item => item.Prompt).ToArray());
    }

    [Fact]
    public async Task StaleSnapshotEditKeepsTheOriginalPromptAndTheEditorText()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        foreach (var text in new[] { "保持原文", "另一条" })
        {
            workspace.ComposerText = text;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        var taskId = workspace.Catalog.SelectedTask!.Id;
        workspace.Queue.Items[0].EditCommand.Execute(null);
        workspace.Queue.Editor!.EditText = "编辑中尚未保存的文字";
        var persisted = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.True(await fixture.Followups.SaveAsync(persisted with
        { Items = [persisted.Items[1], persisted.Items[0]] }, [], fixture.Token));
        await workspace.Queue.SaveEditCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => workspace.Queue.HasNotice);
        Assert.Contains("未覆盖已有内容", workspace.Queue.Notice);
        Assert.Equal("编辑中尚未保存的文字", workspace.Queue.Editor?.EditText);
        var current = await fixture.Followups.GetAsync(taskId, fixture.Token);
        Assert.Equal("保持原文", current.Items.Single(item => item.Prompt != "另一条").Prompt);
    }
}
