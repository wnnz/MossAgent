using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectMetadataTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("MOSS")]
    [InlineData("matching")]
    [InlineData("hidden")]
    [InlineData("missing")]
    public void FilteringDoesNotPublishUnchangedTaskMetadata(string query)
    {
        var project = CreateProject("MossAgent", 1);
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        var visible = CreateTask(project.Id, "matching", 10);
        var hidden = CreateTask(project.Id, "hidden", 15) with { IsPinned = true };
        navigation.SetTasks([visible, hidden]);
        navigation.SetSelectedTask(visible.Id);
        var changes = TrackMetadata(navigation);

        navigation.ApplyFilter(query);

        Assert.Empty(changes);
        Assert.Equal("2 个任务", navigation.ProjectTaskSummary);
        Assert.Equal(hidden.UpdatedAt, navigation.LastActivityAt);
        navigation.ApplyFilter(string.Empty);
        Assert.True(Assert.Single(navigation.Tasks).IsSelected);
        Assert.Empty(changes);
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("upsert")]
    [InlineData("remove")]
    public void TaskMutationsPublishFreshMetadataUnderAnActiveFilter(string operation)
    {
        var project = CreateProject("MossAgent", 1);
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        var visible = CreateTask(project.Id, "matching", 10);
        var hidden = CreateTask(project.Id, "hidden", 15) with { IsPinned = true };
        navigation.SetTasks([visible, hidden]);
        navigation.SetSelectedTask(visible.Id);
        navigation.ApplyFilter("matching");
        var changes = TrackMetadata(navigation);
        var updated = visible with { UpdatedAt = DateTimeOffset.UnixEpoch.AddMinutes(20) };

        if (operation == "replace") navigation.SetTasks([updated, hidden]);
        else if (operation == "upsert") navigation.UpsertTask(updated);
        else navigation.RemoveTask(hidden.Id);

        Assert.Equal([nameof(navigation.ProjectTaskSummary), nameof(navigation.LastActivityAt)], changes);
        Assert.Equal(operation == "remove" ? "1 个任务" : "2 个任务", navigation.ProjectTaskSummary);
        Assert.Equal(operation == "remove" ? visible.UpdatedAt : updated.UpdatedAt, navigation.LastActivityAt);
        Assert.Equal(operation == "remove" ? visible : updated, Assert.Single(navigation.Tasks).Task);
        Assert.True(navigation.Tasks[0].IsSelected);
        Assert.True(navigation.IsVisible);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ProjectUpdatesOnlyPublishChangedFallbackActivity(bool hasTasks, bool creationChanged)
    {
        var project = CreateProject("Original", 1);
        var navigation = new WorkspaceProjectNavigationViewModel(project);
        var task = CreateTask(project.Id, "matching", 10);
        if (hasTasks) navigation.SetTasks([task]);
        navigation.ApplyFilter("Renamed");
        Assert.False(navigation.IsVisible);
        var changes = TrackMetadata(navigation);
        var updated = project with
        {
            Name = "Renamed",
            CreatedAt = creationChanged ? project.CreatedAt.AddMinutes(1) : project.CreatedAt
        };

        navigation.UpdateProject(updated);

        Assert.Equal(!hasTasks && creationChanged ? [nameof(navigation.LastActivityAt)] : [], changes);
        Assert.Equal(hasTasks ? task.UpdatedAt : updated.CreatedAt, navigation.LastActivityAt);
        Assert.Equal(hasTasks ? "1 个任务" : "0 个任务", navigation.ProjectTaskSummary);
        Assert.Equal(hasTasks ? 1 : 0, navigation.Tasks.Count);
        Assert.True(navigation.IsVisible);
    }

    [Fact]
    public void ActivitySortTracksTaskRemovalAndProjectFallbackWithoutSearchNotifications()
    {
        var older = new WorkspaceProjectNavigationViewModel(CreateProject("older", 1));
        var newer = new WorkspaceProjectNavigationViewModel(CreateProject("newer", 2));
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = [older, newer];
        using var list = new WorkspaceSidebarProjectList(source);
        var task = CreateTask(older.Project.Id, "latest", 10);
        older.UpsertTask(task);
        Assert.Equal([older, newer], list.Items);
        var changes = TrackMetadata(older);

        older.ApplyFilter("missing");
        Assert.Equal([older, newer], list.Items);
        Assert.Empty(changes);
        older.RemoveTask(task.Id);
        Assert.Equal([newer, older], list.Items);
        older.UpdateProject(older.Project with { CreatedAt = newer.Project.CreatedAt.AddMinutes(1) });
        Assert.Equal([older, newer], list.Items);
        Assert.Equal([older, newer], source);
    }

    private static List<string> TrackMetadata(WorkspaceProjectNavigationViewModel navigation)
    {
        var changes = new List<string>();
        navigation.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(navigation.ProjectTaskSummary) or nameof(navigation.LastActivityAt))
                changes.Add(args.PropertyName);
        };
        return changes;
    }

    private static ProjectProfile CreateProject(string name, int minutes) =>
        new(Guid.NewGuid(), name, @"D:\synthetic", [], DateTimeOffset.UnixEpoch.AddMinutes(minutes));

    private static AgentTask CreateTask(Guid projectId, string title, int minutes) =>
        new(Guid.NewGuid(), projectId, title, ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(minutes), null);
}
