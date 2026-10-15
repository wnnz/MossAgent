using Avalonia.Headless;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileChangeStatisticsStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"Added\":1}")]
    [InlineData("{\"Added\":-1,\"Removed\":0}")]
    [InlineData("{\"Added\":\"1\",\"Removed\":0}")]
    [InlineData("{\"Added\":2147483647,\"Removed\":0}")]
    [InlineData("{\"Added\":1,\"Added\":2,\"Removed\":0}")]
    [InlineData("truncated {")]
    public Task MalformedResults_AreUnavailableRatherThanInventedZeroTotals(string payload) => _session.DispatchAsync(async () =>
    {
        var executor = new FileStatisticsTestExecutor { Handler = (_, _, _) => Task.FromResult(new ToolResult(true, "statistics", payload)) };
        using var statistics = new WorkspaceFileChangeStatisticsViewModel(new(executor));
        var row = Row();
        statistics.Update([row], Context(), false);
        await WaitAsync(() => row.IsStatisticsResolved && statistics.RetryCommand.CanExecute(null));
        Assert.True(row.IsStatisticsUnavailable);
        Assert.False(statistics.HasTotals);
        Assert.True(statistics.HasError);
        Assert.Equal("未统计", row.UnavailableLabel);
        await statistics.StopAsync();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Retry_OnlyQueriesUnavailableRowsAndRejectsDuplicateActions() => _session.DispatchAsync(async () =>
    {
        var executor = new FileStatisticsTestExecutor { Handler = (_, _, _) => Task.FromResult(new ToolResult(false, "unavailable")) };
        using var statistics = new WorkspaceFileChangeStatisticsViewModel(new(executor));
        var row = Row();
        var context = Context();
        statistics.Update([row], context, false);
        await WaitAsync(() => statistics.RetryCommand.CanExecute(null));
        executor.Handler = (_, _, _) => Task.FromResult(new ToolResult(true, "statistics", "{\"Added\":3,\"Removed\":2}"));
        statistics.RetryCommand.Execute(null);
        statistics.RetryCommand.Execute(null);
        await WaitAsync(() => statistics.HasTotals);
        Assert.Equal(2, executor.Contexts.Count);
        Assert.Equal("+3", statistics.AddedLabel);
        Assert.Equal("-2", statistics.RemovedLabel);
        Assert.Same(row.Change, executor.Contexts.Last().ReviewedChange);
        Assert.Equal(TimeSpan.FromSeconds(5), executor.Contexts.Last().Timeout);
        statistics.Update([row], context, true);
        Assert.False(statistics.HasTotals);
        Assert.True(row.HasStatistics);
        await statistics.StopAsync();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ChangedTask_CancelsOldWorkAndRejectsItsLateResult() => _session.DispatchAsync(async () =>
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstContext = Context();
        var executor = new FileStatisticsTestExecutor
        {
            Handler = (_, context, _) =>
            {
                if (context.TaskId != firstContext.TaskId) return Task.FromResult(new ToolResult(true, "statistics", "{\"Added\":3,\"Removed\":2}"));
                started.TrySetResult();
                return release.Task;
            }
        };
        using var statistics = new WorkspaceFileChangeStatisticsViewModel(new(executor));
        var first = Row();
        var second = Row();
        statistics.Update([first], firstContext, false);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        statistics.Update([second], Context(), false);
        Assert.Single(executor.Contexts);
        release.SetResult(new(true, "late", "{\"Added\":999,\"Removed\":999}"));
        await WaitAsync(() => second.HasStatistics);
        Assert.False(first.IsStatisticsResolved);
        Assert.Equal("+3", statistics.AddedLabel);
        Assert.Equal(2, executor.Contexts.Count);
        await statistics.StopAsync();
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task StopOrDispose_CancelsTheOwnedQueryAndPreventsAnyMorePublication(bool dispose) => _session.DispatchAsync(async () =>
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new FileStatisticsTestExecutor
        {
            Handler = async (_, _, token) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new(true, "unreachable", "{\"Added\":1,\"Removed\":1}");
            }
        };
        using var statistics = new WorkspaceFileChangeStatisticsViewModel(new(executor));
        var row = Row();
        statistics.Update([row], Context(), false);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        if (dispose) statistics.Dispose();
        await statistics.StopAsync();
        statistics.Update([Row()], Context(), false);
        statistics.RetryCommand.Execute(null);
        Assert.Single(executor.Contexts);
        Assert.False(row.HasStatistics);
        Assert.False(statistics.HasTotals);
        Assert.False(statistics.RetryCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Totals_PublishConsistentChangesWithoutRequeryingResolvedRows() => _session.DispatchAsync(async () =>
    {
        var executor = new FileStatisticsTestExecutor();
        using var statistics = new WorkspaceFileChangeStatisticsViewModel(new(executor));
        var context = Context();
        statistics.Update([], context, false);
        var first = Row();
        var second = Row();
        first.SetStatistics(new(3, 2), true);
        second.SetStatistics(new(0, 0), true);
        var changes = new List<string?>();
        statistics.PropertyChanged += (_, args) =>
        {
            changes.Add(args.PropertyName);
            Assert.Equal(statistics.HasTotals ? "+3" : "+0", statistics.AddedLabel);
            Assert.Equal(statistics.HasTotals ? "-2" : "-0", statistics.RemovedLabel);
        };
        statistics.Update([first, second], context, false);
        statistics.Update([first, second], context, false);
        Assert.Equal([nameof(statistics.HasTotals), nameof(statistics.AddedLabel), nameof(statistics.RemovedLabel)], changes);
        statistics.Update([first, second], context, true);
        Assert.False(statistics.HasTotals);
        statistics.Update([first, second], context, false);
        await WaitAsync(() => statistics.HasTotals);
        await statistics.StopAsync();
        Assert.Equal(9, changes.Count);
        Assert.Empty(executor.Contexts);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void EquivalentRowStatistics_DoNotRefreshBindings()
    {
        var row = Row();
        var changes = new List<string?>();
        row.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        row.SetStatistics(null, false);
        Assert.Empty(changes);
        row.SetStatistics(new(3, 2), true);
        Assert.Equal(4, changes.Count);
        changes.Clear();
        row.SetStatistics(new(3, 2), true);
        Assert.Empty(changes);
        row.SetStatistics(null, true);
        Assert.True(row.IsStatisticsUnavailable);
        Assert.Equal(4, changes.Count);
        changes.Clear();
        row.SetStatistics(null, true);
        Assert.Empty(changes);
    }

    private static WorkspaceFileChangeViewModel Row() => new(new(Path.Combine(Path.GetTempPath(), "statistics.cs"), new string('A', 64), new string('B', 64)), "statistics.cs");
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);

    internal static async Task WaitAsync(Func<bool> ready)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!ready()) await Task.Delay(10, timeout.Token);
    }
}
