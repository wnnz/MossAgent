using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class WorkspaceTaskLookupOptimizationTests(ITestOutputHelper output)
{
    [Fact]
    public void Lookup_UsesTheFullCurrentSnapshotAndFirstDuplicate()
    {
        var project = new ProjectProfile(Guid.NewGuid(), "synthetic", "D:\\synthetic", [], DateTimeOffset.UnixEpoch);
        var first = CreateTask(project.Id, "first") with { IsPinned = true };
        var duplicate = first with { Title = "duplicate" };
        var other = CreateTask(project.Id, "other");
        var group = new WorkspaceProjectNavigationViewModel(project);
        group.SetTasks([first, duplicate, other]);
        group.ApplyFilter("missing");

        Assert.Empty(group.Tasks);
        Assert.Same(first, group.FindTask(first.Id));
        Assert.Same(other, group.FindTask(other.Id));
        Assert.Null(group.FindTask(Guid.NewGuid()));
        group.SetTasks([duplicate, first]);
        Assert.Same(duplicate, group.FindTask(first.Id));
        var updated = first with { Title = "updated" };
        group.UpsertTask(updated);
        Assert.Same(updated, group.FindTask(first.Id));
        group.RemoveTask(first.Id);
        Assert.Null(group.FindTask(first.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmTaskLookup_DoesNotAllocate(bool missing)
    {
        var project = new ProjectProfile(Guid.NewGuid(), "synthetic", "D:\\synthetic", [], DateTimeOffset.UnixEpoch);
        var tasks = Enumerable.Range(0, 32).Select(index => CreateTask(project.Id, $"task {index}")).ToArray();
        var group = new WorkspaceProjectNavigationViewModel(project);
        group.SetTasks(tasks);
        var taskId = missing ? Guid.NewGuid() : tasks[^1].Id;
        var valid = true;
        for (var index = 0; index < 1_000; index++) valid &= ReferenceEquals(missing ? null : tasks[^1], group.FindTask(taskId));
        var allocated = AllocationMeasurement.Measure(() =>
        {
            for (var index = 0; index < 10_000; index++) valid &= ReferenceEquals(missing ? null : tasks[^1], group.FindTask(taskId));
        });
        output.WriteLine($"10,000 task lookups, missing={missing}: {allocated} bytes");
        Assert.True(valid);
        Assert.Equal(0, allocated);
    }

    private static AgentTask CreateTask(Guid projectId, string title) => new(
        Guid.NewGuid(), projectId, title, ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
}
