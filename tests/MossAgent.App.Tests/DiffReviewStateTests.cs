using Avalonia.Headless;
using Avalonia.Threading;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DiffReviewStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.ReadOnly);

    [Fact]
    public Task PendingAndFailedReadsDoNotRenderDiagnosticTextAsADiff() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => request.ToolName == "git.status"
                ? Task.FromResult(ToolResult.Success("status", " M selected.cs\0")) : pending.Task
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.True(pane.IsDiffLoading);
        Assert.False(pane.HasContent);
        Assert.Contains("正在读取", pane.ContentStatus);
        pending.SetResult(ToolResult.Failure("secret credential exception", "secret-code"));
        await DrainAsync();
        Assert.False(pane.IsDiffLoading);
        Assert.False(pane.HasContent);
        Assert.Contains("权限", pane.ContentStatus);
        Assert.DoesNotContain("secret", pane.Content + pane.ContentStatus);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ChangingTheFilterUpdatesCountsAndEmptyState() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => Task.FromResult(request.ToolName == "git.status"
                ? ToolResult.Success("status", " M selected.cs\0") : ToolResult.Success("diff", "content"))
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("显示 1", pane.Status);
        pane.IsStaged = true;
        Assert.Contains("显示 0", pane.Status);
        Assert.Empty(pane.Files);
        Assert.False(pane.HasContent);
        Assert.Equal("没有已暂存变更。", pane.ContentStatus);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EmptyTextDiffIsAnEmptyStateInsteadOfSyntheticCode() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => Task.FromResult(request.ToolName == "git.status"
                ? ToolResult.Success("status", " M selected.cs\0") : ToolResult.Success("diff", string.Empty))
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.False(pane.HasContent);
        Assert.Contains("没有可显示", pane.ContentStatus);
    }, TestContext.Current.CancellationToken);

    private static async Task DrainAsync()
    {
        for (var count = 0; count < 3; count++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
