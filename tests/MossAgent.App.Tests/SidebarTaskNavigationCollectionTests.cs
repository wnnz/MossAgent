using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarTaskNavigationCollectionTests(ITestOutputHelper output)
{
    [Fact]
    public void ShortDuplicateSequences_RetainRowsByOccurrenceAndMatchEveryTransition()
    {
        var tasks = Enumerable.Range(0, 3).Select(CreateTask).ToArray();
        var snapshots = Snapshots(tasks).ToArray();
        foreach (var initial in snapshots)
            foreach (var desired in snapshots)
            {
                var target = new ObservableCollection<WorkspaceTaskNavigationViewModel>();
                target.ReplaceTasks(initial, null);
                var original = target.GroupBy(row => row.Task)
                    .ToDictionary(group => group.Key, group => new Queue<WorkspaceTaskNavigationViewModel>(group));
                var notifications = new List<NotifyCollectionChangedAction>();
                target.CollectionChanged += (_, args) => notifications.Add(args.Action);

                target.ReplaceTasks(desired, tasks[0].Id);

                Assert.Equal(desired, target.Select(row => row.Task));
                Assert.Equal(target.Count, target.Distinct().Count());
                for (var index = 0; index < target.Count; index++)
                {
                    if (original.TryGetValue(desired[index], out var candidates) && candidates.TryDequeue(out var row))
                        Assert.Same(row, target[index]);
                    Assert.Equal(target[index].Task.Id == tasks[0].Id, target[index].IsSelected);
                }
                Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, notifications);
                Assert.DoesNotContain(NotifyCollectionChangedAction.Replace, notifications);
            }
    }

    [Fact]
    public void EqualRecords_PreservePrefixAndPublishOnlyTailChanges()
    {
        var first = CreateTask(0);
        var second = CreateTask(1);
        var target = new ObservableCollection<WorkspaceTaskNavigationViewModel>();
        target.ReplaceTasks([first], first.Id);
        var retained = Assert.Single(target);
        var notifications = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, args) => notifications.Add(args.Action);

        target.ReplaceTasks([first with { }, second], first.Id);
        target.ReplaceTasks([first with { }], null);

        Assert.Same(retained, Assert.Single(target));
        Assert.False(retained.IsSelected);
        Assert.Equal([NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Remove], notifications);
    }

    [Fact]
    public void WarmAppendAndRemove_AvoidRebuildingUnchangedRowCandidates()
    {
        var snapshot = Enumerable.Range(0, 32).Select(CreateTask).ToArray();
        var extended = snapshot.Append(CreateTask(32)).ToArray();
        var target = new ObservableCollection<WorkspaceTaskNavigationViewModel>();
        target.ReplaceTasks(snapshot, snapshot[0].Id);
        var original = target.ToArray();
        for (var index = 0; index < 100; index++)
        {
            target.ReplaceTasks(extended, snapshot[0].Id);
            target.ReplaceTasks(snapshot, snapshot[0].Id);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 1_000; index++)
        {
            target.ReplaceTasks(extended, snapshot[0].Id);
            target.ReplaceTasks(snapshot, snapshot[0].Id);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Append/remove rows (1,000 cycles): {allocated} bytes");
        Assert.Equal(snapshot, target.Select(row => row.Task));
        for (var index = 0; index < original.Length; index++) Assert.Same(original[index], target[index]);
        Assert.True(target[0].IsSelected);
        Assert.InRange(allocated, 0, 1_024_000);
    }

    private static IEnumerable<AgentTask[]> Snapshots(AgentTask[] tasks)
    {
        yield return [];
        for (var length = 1; length <= 4; length++)
            for (var encoded = 0; encoded < Math.Pow(tasks.Length, length); encoded++)
            {
                var snapshot = new AgentTask[length];
                var remainder = encoded;
                for (var index = 0; index < length; index++)
                {
                    snapshot[index] = tasks[remainder % tasks.Length] with { };
                    remainder /= tasks.Length;
                }
                yield return snapshot;
            }
    }

    private static AgentTask CreateTask(int index) => new(
        Guid.NewGuid(), Guid.NewGuid(), $"task {index}", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(index), null);
}
