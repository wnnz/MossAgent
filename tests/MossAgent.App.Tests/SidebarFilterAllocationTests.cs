using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class SidebarFilterAllocationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("pinned", false)]
    [InlineData("recent", false)]
    [InlineData("project", false)]
    [InlineData("pinned", true)]
    [InlineData("recent", true)]
    [InlineData("project", true)]
    public void WarmUnchangedFilters_DoNotAllocateOrReplaceRows(string kind, bool noMatches)
    {
        var projectId = Guid.NewGuid();
        var tasks = Enumerable.Range(0, 32).Select(index => CreateTask(projectId, index, kind == "pinned")).ToArray();
        var (rows, filter, select) = CreateList(kind, projectId, tasks);
        var query = noMatches ? "missing" : "match";
        select(tasks[0].Id);
        filter(query);
        var original = rows.ToArray();
        var notifications = 0;
        rows.CollectionChanged += (_, _) => notifications++;
        for (var index = 0; index < 100; index++) filter(query);
        var allocated = AllocationMeasurement.Measure(() =>
        {
            for (var index = 0; index < 10_000; index++) filter(query);
        });
        output.WriteLine($"{kind}, noMatches={noMatches} (10,000 filters): {allocated} bytes");
        Assert.Equal(noMatches ? 0 : tasks.Length, rows.Count);
        for (var index = 0; index < original.Length; index++) Assert.Same(original[index], rows[index]);
        Assert.Equal(noMatches ? 0 : 1, rows.Count(row => row.IsSelected));
        Assert.Equal(0, notifications);
        Assert.Equal(0, allocated);
    }

    private static (ObservableCollection<WorkspaceTaskNavigationViewModel> Rows,
        Action<string> Filter, Action<Guid?> Select) CreateList(string kind, Guid projectId, AgentTask[] tasks)
    {
        if (kind == "pinned")
        {
            var model = new WorkspacePinnedTasksViewModel();
            model.SetTasks(tasks, string.Empty);
            return (model.Items, model.ApplyFilter, model.SetSelectedTask);
        }
        if (kind == "recent")
        {
            var model = new WorkspaceRecentTasksViewModel();
            model.SetTasks(tasks, string.Empty);
            return (model.Items, model.ApplyFilter, model.SetSelectedTask);
        }
        var project = new ProjectProfile(projectId, "project", "D:\\synthetic", [], DateTimeOffset.UnixEpoch);
        var group = new WorkspaceProjectNavigationViewModel(project);
        group.SetTasks(tasks);
        return (group.Tasks, group.ApplyFilter, group.SetSelectedTask);
    }

    private static AgentTask CreateTask(Guid projectId, int index, bool pinned) => new(
        Guid.NewGuid(), projectId, $"match {index}", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(index), null, IsPinned: pinned);
}
