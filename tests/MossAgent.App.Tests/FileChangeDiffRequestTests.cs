using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileChangeDiffRequestTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task Review_RejectsWrongTaskAndDisposedPane() => _session.DispatchAsync(async () =>
    {
        var calls = 0;
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => { calls++; return Task.FromResult(ToolResult.Success("ok")); } };
        var context = Context();
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(context);
        await pane.ReviewAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        pane.Dispose();
        await pane.ReviewAsync(context.TaskId, TestContext.Current.CancellationToken);
        Assert.Equal(0, calls);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancelledReview_ReturnsToAnActionableRefreshState() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = async (_, _, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return ToolResult.Success("unused"); } };
        var context = Context();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(context);
        var reviewing = pane.ReviewAsync(context.TaskId, cancellation.Token);
        Assert.True(pane.IsLoading);
        cancellation.Cancel();
        await reviewing;
        Assert.False(pane.IsLoading);
        Assert.True(pane.RefreshCommand.CanExecute(null));
        Assert.DoesNotContain("正在读取", pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SwitchedTask_DiscardsALateStatusSnapshot() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task };
        var context = Context();
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(context);
        var reviewing = pane.ReviewAsync(context.TaskId, TestContext.Current.CancellationToken);
        pane.Attach(Context());
        pending.SetResult(ToolResult.Success("old", " M old.cs\0"));
        await reviewing;
        Assert.Empty(pane.Files);
        Assert.False(pane.HasContent);
        Assert.False(pane.IsLoading);
    }, TestContext.Current.CancellationToken);

    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.ReadOnly);
}
