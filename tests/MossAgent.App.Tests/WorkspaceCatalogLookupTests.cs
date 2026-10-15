using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceCatalogLookupTests(ITestOutputHelper output)
{
    [Fact]
    public void ProjectLookups_FollowTheFirstCurrentOccurrenceWithoutCaching()
    {
        var first = Project("first");
        var duplicate = first with { Name = "duplicate" };
        var other = Project("other");
        ObservableCollection<ProjectProfile> projects = [first, other, duplicate];

        Assert.Null(projects.FindProject(null));
        Assert.Equal(-1, projects.FindProjectIndex(null));
        Assert.Null(projects.FindProject(Guid.NewGuid()));
        Assert.Same(first, projects.FindProject(first.Id));
        Assert.Equal(0, projects.FindProjectIndex(first.Id));
        projects.Move(2, 0);
        Assert.Same(duplicate, projects.FindProject(first.Id));
        Assert.Equal(2, projects.FindProjectIndex(other.Id));
        var updated = other with { Name = "updated" };
        projects[2] = updated;
        Assert.Same(updated, projects.FindProject(other.Id));
        projects.Remove(updated);
        Assert.Null(projects.FindProject(other.Id));
        Assert.Equal(-1, projects.FindProjectIndex(other.Id));
        projects.Clear();
        Assert.Null(projects.FindProject(first.Id));
    }

    [Fact]
    public void GroupLookups_RespectProjectOwnershipAndDoNotFallBackToDuplicateGroups()
    {
        var project = Project("first");
        var other = Project("other");
        var first = new WorkspaceProjectNavigationViewModel(project);
        var duplicate = new WorkspaceProjectNavigationViewModel(project with { Name = "duplicate" });
        var foreign = new WorkspaceProjectNavigationViewModel(other);
        var task = CreateTask(project.Id);
        var foreignTask = task with { ProjectId = other.Id };
        duplicate.SetTasks([task]);
        foreign.SetTasks([foreignTask]);
        ObservableCollection<WorkspaceProjectNavigationViewModel> groups = [first, duplicate, foreign];

        Assert.Null(groups.FindProjectGroup(null));
        Assert.Null(groups.FindProjectGroup(Guid.NewGuid()));
        Assert.Same(first, groups.FindProjectGroup(project.Id));
        Assert.Null(groups.FindProjectGroup(project.Id)?.FindTask(task.Id));
        Assert.Same(foreignTask, groups.FindProjectGroup(other.Id)?.FindTask(task.Id));
        groups.Move(1, 0);
        Assert.Same(task, groups.FindProjectGroup(project.Id)?.FindTask(task.Id));
        groups.Remove(duplicate);
        Assert.Null(groups.FindProjectGroup(project.Id)?.FindTask(task.Id));
        groups.Clear();
        Assert.Null(groups.FindProjectGroup(project.Id));
    }

    [Fact]
    public void TaskLookup_AcceptsArraySnapshotsAndRejectsEmptyOrMissingIdentities()
    {
        var first = CreateTask(Guid.NewGuid());
        var duplicate = first with { Title = "duplicate" };
        AgentTask[] tasks = [first, duplicate];

        Assert.Same(first, tasks.FindTask(first.Id));
        Assert.Null(tasks.FindTask(null));
        Assert.Null(tasks.FindTask(Guid.NewGuid()));
        tasks[0] = duplicate;
        Assert.Same(duplicate, tasks.FindTask(first.Id));
        Assert.Null(Array.Empty<AgentTask>().FindTask(first.Id));
        Assert.Null(Array.Empty<ProjectProfile>().FindProject(Guid.NewGuid()));
        Assert.Null(Array.Empty<WorkspaceProjectNavigationViewModel>().FindProjectGroup(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmCatalogLookups_DoNotAllocate(bool missing)
    {
        ObservableCollection<ProjectProfile> projects = new(Enumerable.Range(0, 32).Select(index => Project($"project {index}")));
        ObservableCollection<WorkspaceProjectNavigationViewModel> groups = new(projects.Select(project => new WorkspaceProjectNavigationViewModel(project)));
        var projectId = missing ? Guid.NewGuid() : projects[^1].Id;
        var valid = true;
        for (var index = 0; index < 1_000; index++) valid &= Check(projects, groups, projectId, missing);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++) valid &= Check(projects, groups, projectId, missing);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"10,000 combined project/index/group lookups, missing={missing}: {allocated} bytes");
        Assert.True(valid);
        Assert.Equal(0, allocated);
    }

    private static bool Check(ObservableCollection<ProjectProfile> projects,
        ObservableCollection<WorkspaceProjectNavigationViewModel> groups, Guid projectId, bool missing) =>
        ReferenceEquals(missing ? null : projects[^1], projects.FindProject(projectId))
        && projects.FindProjectIndex(projectId) == (missing ? -1 : projects.Count - 1)
        && ReferenceEquals(missing ? null : groups[^1], groups.FindProjectGroup(projectId));

    private static ProjectProfile Project(string name) => new(
        Guid.NewGuid(), name, "D:\\synthetic", [], DateTimeOffset.UnixEpoch);

    private static AgentTask CreateTask(Guid projectId) => new(
        Guid.NewGuid(), projectId, "synthetic", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
}
