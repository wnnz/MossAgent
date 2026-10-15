using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ObservableCollectionSynchronizationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(new int[] { }, new int[] { })]
    [InlineData(new int[] { }, new[] { 1, 2 })]
    [InlineData(new[] { 1, 2 }, new int[] { })]
    [InlineData(new[] { 1, 2 }, new[] { 1, 2 })]
    [InlineData(new[] { 1, 2 }, new[] { 1, 2, 3 })]
    [InlineData(new[] { 1, 2, 3 }, new[] { 1, 2 })]
    [InlineData(new[] { 1, 2 }, new[] { 3, 1, 2 })]
    [InlineData(new[] { 1, 2, 3 }, new[] { 2, 3 })]
    [InlineData(new[] { 1, 2, 3 }, new[] { 1, 3, 2 })]
    [InlineData(new[] { 1, 2, 3 }, new[] { 1, 4, 3 })]
    [InlineData(new[] { 1, 1, 2 }, new[] { 1, 2 })]
    [InlineData(new[] { 1, 2 }, new[] { 1, 1, 2 })]
    [InlineData(new[] { 1, 2, 1 }, new[] { 1, 1 })]
    public void SynchronizeWith_MatchesSnapshotWithoutReset(int[] initial, int[] desired)
    {
        var target = new ObservableCollection<int>(initial);
        var actions = Observe(target);

        target.SynchronizeWith(desired);

        Assert.Equal(desired, target);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Replace, actions);
    }

    [Fact]
    public void PrefixChanges_PublishOnlyTailAddAndRemove()
    {
        var target = new ObservableCollection<int> { 1, 2 };
        var actions = Observe(target);

        target.SynchronizeWith(new[] { 1, 2, 3 });
        target.SynchronizeWith(new[] { 1, 2 });

        Assert.Equal(new[] { 1, 2 }, target);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Remove }, actions);
    }

    [Fact]
    public void Reordering_PreservesPrefixAndDuplicateInstances()
    {
        var prefix = new object();
        var first = new object();
        var second = new object();
        var target = new ObservableCollection<object> { prefix, first, first, second };
        var actions = Observe(target);

        target.SynchronizeWith(new[] { prefix, second, first, first });

        Assert.Same(prefix, target[0]);
        Assert.Same(second, target[1]);
        Assert.Same(first, target[2]);
        Assert.Same(first, target[3]);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Move }, actions);
    }

    [Fact]
    public void NullDuplicates_AreRetainedAndRemovedByOccurrence()
    {
        var target = new ObservableCollection<string?> { null, "value", null };

        target.SynchronizeWith(new string?[] { null, null });

        Assert.Equal(new string?[] { null, null }, target);
    }

    [Fact]
    public void EqualValues_PreserveExistingReferencesWithoutNotifications()
    {
        var original = new string('x', 4);
        var target = new ObservableCollection<string> { original };
        var actions = Observe(target);

        target.SynchronizeWith(new[] { new string('x', 4) });
        target.SynchronizeWith(target);

        Assert.Same(original, Assert.Single(target));
        Assert.Empty(actions);
    }

    [Fact]
    public void WarmUnchangedSnapshots_DoNotAllocateOrPublishNotifications()
    {
        var snapshot = Enumerable.Range(0, 32).ToArray();
        var target = new ObservableCollection<int>(snapshot);
        var actions = Observe(target);
        for (var index = 0; index < 100; index++) target.SynchronizeWith(snapshot);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++) target.SynchronizeWith(snapshot);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Unchanged snapshots (10,000): {allocated} bytes");
        Assert.Equal(snapshot, target);
        Assert.Empty(actions);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void WarmPrefixChanges_AvoidRebuildingMembershipSets()
    {
        var snapshot = Enumerable.Range(0, 32).ToArray();
        var extended = Enumerable.Range(0, 33).ToArray();
        var target = new ObservableCollection<int>(snapshot);
        var notifications = 0;
        target.CollectionChanged += (_, _) => notifications++;
        for (var index = 0; index < 100; index++)
        {
            target.SynchronizeWith(extended);
            target.SynchronizeWith(snapshot);
        }
        notifications = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 1_000; index++)
        {
            target.SynchronizeWith(extended);
            target.SynchronizeWith(snapshot);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Append/remove cycles (1,000): {allocated} bytes");
        Assert.Equal(snapshot, target);
        Assert.Equal(2_000, notifications);
        Assert.InRange(allocated, 0, 512_000);
    }

    [Fact]
    public void ShortDuplicateSequences_MatchEveryRequestedTransition()
    {
        var snapshots = Snapshots().ToArray();
        foreach (var initial in snapshots)
            foreach (var desired in snapshots)
            {
                var target = new ObservableCollection<int>(initial);

                target.SynchronizeWith(desired);

                Assert.Equal(desired, target);
            }
    }

    private static IEnumerable<int[]> Snapshots()
    {
        yield return [];
        for (var length = 1; length <= 4; length++)
            for (var encoded = 0; encoded < Math.Pow(3, length); encoded++)
            {
                var snapshot = new int[length];
                var remainder = encoded;
                for (var index = 0; index < length; index++)
                {
                    snapshot[index] = remainder % 3;
                    remainder /= 3;
                }
                yield return snapshot;
            }
    }

    private static List<NotifyCollectionChangedAction> Observe<T>(ObservableCollection<T> target)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, args) => actions.Add(args.Action);
        return actions;
    }
}
