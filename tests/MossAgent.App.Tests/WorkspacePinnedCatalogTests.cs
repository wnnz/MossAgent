using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePinnedCatalogTests
{
    [Fact]
    public void RuntimeTaskUpdatesCannotRestoreStalePinState()
    {
        var repository = new TestWorkspaceRepository();
        var catalog = new WorkspaceCatalogViewModel(repository,
            null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        var task = new AgentTask(
            Guid.NewGuid(), Guid.NewGuid(), "review", ApprovalPolicy.AskEveryTime,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        catalog.TaskManagement.PinnedTasks.SetTasks([task with { IsPinned = true }], "");
        catalog.UpsertTask(task, select: false);
        Assert.True(catalog.TaskManagement.PinnedTasks.IsPinned(task.Id));
        Assert.Empty(catalog.RecentTasks.Items);

        catalog.TaskManagement.PinnedTasks.Upsert(task);
        catalog.UpsertTask(task with { IsPinned = true }, select: false);
        Assert.False(catalog.TaskManagement.PinnedTasks.IsPinned(task.Id));
        Assert.Equal(task.Id, Assert.Single(catalog.RecentTasks.Items).Task.Id);
    }
}
