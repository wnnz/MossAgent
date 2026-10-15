using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceTaskSearchSnapshotTests
{
    [Fact]
    public void EquivalentSnapshotsAndQueries_RetainInstancesWithoutNotifications()
    {
        var task = CreateTask("matching task");
        var search = new WorkspaceTaskSearchViewModel();
        search.SetItems([task]);
        var changes = Observe(search);

        search.Query = "  matching  ";
        search.SetItems([task with { }]);
        search.Upsert(task with { });
        search.Remove(Guid.NewGuid());

        Assert.Same(task, Assert.Single(search.Results));
        Assert.Empty(changes);
        Assert.Equal("  matching  ", search.Query);
        Assert.True(search.HasQuery);
        Assert.True(search.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void HiddenTaskChanges_DoNotRepublishVisibleResults()
    {
        var visible = CreateTask("matching task");
        var hidden = CreateTask("hidden task");
        var search = new WorkspaceTaskSearchViewModel { Query = "matching" };
        search.SetItems([visible, hidden]);
        var changes = Observe(search);

        search.Upsert(hidden with { Status = AgentTaskStatus.Running });
        search.Upsert(CreateTask("another hidden task"));
        search.Remove(hidden.Id);

        Assert.Same(visible, Assert.Single(search.Results));
        Assert.Empty(changes);
    }

    [Fact]
    public void ChangedTask_ReplacesOnlyItsPositionAndPublishesCurrentMetadata()
    {
        var first = CreateTask("first");
        var second = CreateTask("second");
        var search = new WorkspaceTaskSearchViewModel();
        search.SetItems([first, second]);
        var changes = Observe(search);
        var updated = first with { Title = "renamed", Status = AgentTaskStatus.Running, IsPinned = true };

        search.Upsert(updated);

        Assert.Same(updated, search.Results[0]);
        Assert.Same(second, search.Results[1]);
        var change = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Replace, change.Action);
        Assert.Equal(0, change.NewStartingIndex);
        Assert.Same(updated, Assert.Single(change.NewItems!.Cast<AgentTask>()));
        Assert.Same(first, Assert.Single(change.OldItems!.Cast<AgentTask>()));
    }

    [Fact]
    public void FilteringAndClearing_UseDeltasAndPreserveSourceOrder()
    {
        var first = CreateTask("first");
        var second = CreateTask("second");
        var search = new WorkspaceTaskSearchViewModel();
        search.SetItems([first, second]);
        var changes = Observe(search);

        search.Query = "second";
        Assert.Equal([second], search.Results);
        search.Query = "missing";
        Assert.Empty(search.Results);
        search.ClearCommand.Execute(null);

        Assert.Equal([first, second], search.Results);
        Assert.DoesNotContain(changes, change => change.Action == NotifyCollectionChangedAction.Reset);
        Assert.False(search.HasQuery);
        Assert.False(search.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void ReorderedSnapshot_RetainsEqualRowsAtUnchangedPositions()
    {
        var first = CreateTask("first");
        var second = CreateTask("second");
        var third = CreateTask("third");
        var search = new WorkspaceTaskSearchViewModel();
        search.SetItems([first, second, third]);
        var changes = Observe(search);

        search.SetItems([second, first, third with { }]);

        Assert.Equal([second, first, third], search.Results);
        Assert.Same(third, search.Results[2]);
        Assert.Equal(2, changes.Count);
        Assert.All(changes, change => Assert.Equal(NotifyCollectionChangedAction.Replace, change.Action));
    }

    [Fact]
    public void DuplicateTaskSnapshots_AreNotSilentlyDeduplicated()
    {
        var task = CreateTask("matching task");
        var search = new WorkspaceTaskSearchViewModel();
        search.SetItems([task, task with { }]);
        var retained = search.Results.ToArray();
        var changes = Observe(search);

        search.SetItems([task with { }, task with { }]);
        search.Query = "matching";

        Assert.Equal(2, search.Results.Count);
        Assert.Same(retained[0], search.Results[0]);
        Assert.Same(retained[1], search.Results[1]);
        Assert.Empty(changes);
        search.Remove(task.Id);
        Assert.Empty(search.Results);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" task ")]
    [InlineData("TASK")]
    [InlineData("其他")]
    [InlineData("missing")]
    public void MutationSequence_MatchesTheExistingFilterAndOrdering(string query)
    {
        var expected = Enumerable.Range(0, 12).Select(index => CreateTask($"task {index}")).ToList();
        var search = new WorkspaceTaskSearchViewModel { Query = query };
        search.SetItems(expected);
        for (var index = 0; index < 12; index++)
        {
            var updated = expected[index] with { Title = index % 2 == 0 ? $"其他 {index}" : $"task {index}" };
            expected[index] = updated;
            search.Upsert(updated);
            AssertMatches(search, expected);
        }
        var added = CreateTask("task new");
        expected.Insert(0, added);
        search.Upsert(added);
        AssertMatches(search, expected);
        var removed = expected[3];
        expected.RemoveAt(3);
        search.Remove(removed.Id);
        AssertMatches(search, expected);
        expected.Reverse();
        search.SetItems(expected.Select(task => task with { }));
        AssertMatches(search, expected);
        search.ClearCommand.Execute(null);
        AssertMatches(search, expected);
    }

    private static void AssertMatches(WorkspaceTaskSearchViewModel search, IReadOnlyList<AgentTask> tasks) =>
        Assert.Equal(tasks.Where(task => task.Title.Contains(search.Query.Trim(),
            StringComparison.CurrentCultureIgnoreCase)), search.Results);

    private static List<NotifyCollectionChangedEventArgs> Observe(WorkspaceTaskSearchViewModel search)
    {
        var changes = new List<NotifyCollectionChangedEventArgs>();
        search.Results.CollectionChanged += (_, args) => changes.Add(args);
        return changes;
    }

    private static AgentTask CreateTask(string title) => new(Guid.NewGuid(), Guid.NewGuid(), title,
        ApprovalPolicy.AskEveryTime, AgentTaskStatus.Completed, DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch, null);
}
