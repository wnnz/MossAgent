using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarProjectSubscriptionAllocationTests(ITestOutputHelper output)
{
    private const int Iterations = 1_000;

    [Theory]
    [InlineData(32)]
    [InlineData(256)]
    public void SourceMoves_AddOnlyBoundedEventOverheadToEquivalentSortRefreshes(int projectCount)
    {
        ObservableCollection<WorkspaceProjectNavigationViewModel> source = new(
            Enumerable.Range(0, projectCount).Select(index => SidebarProjectOrderingTests.Project($"project {index}", index)));
        using var list = new WorkspaceSidebarProjectList(source);
        var preference = new SidebarPreference(SidebarProjectSortOrder.UpdatedAt, []);
        list.SetPreference(preference);
        var original = list.Items.ToArray();
        var notifications = 0;
        list.Items.CollectionChanged += (_, _) => notifications++;
        var refreshBytes = Measure(() =>
        {
            list.SetPreference(preference);
            list.SetPreference(preference);
        });

        var moveBytes = Measure(() =>
        {
            source.Move(0, projectCount - 1);
            source.Move(projectCount - 1, 0);
        });

        output.WriteLine($"{projectCount} projects, {Iterations * 2} refreshes: {refreshBytes} bytes; moves: {moveBytes} bytes");
        Assert.Equal(original, list.Items);
        Assert.Equal(0, notifications);
        Assert.InRange(moveBytes - refreshBytes, 0, Iterations * 2L * 512);
    }

    private static long Measure(Action action)
    {
        for (var index = 0; index < 100; index++) action();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < Iterations; index++) action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
