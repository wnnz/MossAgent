using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitIndexActionsTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);

    [Theory]
    [InlineData("R ", true, "source.txt")]
    [InlineData(" R", false, "source.txt")]
    [InlineData("RM", false, null)]
    [InlineData("C ", true, null)]
    [InlineData(" C", false, null)]
    public Task OnlyTheChosenRenameSideIncludesTheSourceFile(string status, bool staged, string? original) => _session.DispatchAsync(async () =>
    {
        ToolRequest? captured = null;
        var refreshes = 0;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        { captured = request; return Task.FromResult(ToolResult.Success("done")); } };
        await using var actions = new GitIndexActionsViewModel(executor, _ => { refreshes++; return Task.CompletedTask; });
        actions.Attach(Context());
        actions.Select(new("destination.txt", status, true, true, "source.txt"), staged);
        await actions.ExecuteCommand.ExecuteAsync(null);
        Assert.NotNull(captured);
        Assert.Equal(staged ? "git.unstage_file" : "git.stage_file", captured.ToolName);
        Assert.Equal("destination.txt", captured.Arguments.GetProperty("path").GetString());
        Assert.Equal(original, captured.Arguments.GetProperty("originalPath").GetString());
        Assert.Equal(1, refreshes);
        Assert.False(actions.IsBusy);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("approval_denied", "DiffIndexDenied")]
    [InlineData("path_denied", "DiffIndexPathDenied")]
    [InlineData("workspace_denied", "DiffIndexPathDenied")]
    [InlineData("tool_timeout", "DiffIndexInterrupted")]
    [InlineData("git_failed", "DiffIndexFailed")]
    public Task FailureIsActionableDoesNotLeakOutputAndDoesNotRefresh(string error, string key) => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(ToolResult.Failure("secret-token", error)) };
        await using var actions = new GitIndexActionsViewModel(executor, _ => throw new InvalidOperationException("Must not refresh."));
        actions.Attach(Context());
        actions.Select(new("selected.txt", " M", false, true), false);
        await actions.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(InspectorText.Get(key), actions.Status);
        Assert.DoesNotContain("secret", actions.Status);
        Assert.True(actions.ExecuteCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReadOnlyDisabledToolsAndAbsentSelectionCannotExecute() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => throw new InvalidOperationException("Must not execute.") };
        await using var actions = new GitIndexActionsViewModel(executor, _ => Task.CompletedTask);
        foreach (var context in new[] { Context() with { ApprovalPolicy = ApprovalPolicy.ReadOnly }, Context() with { ToolsEnabled = false } })
        {
            actions.Attach(context);
            actions.Select(new("selected.txt", " M", false, true), false);
            Assert.False(actions.ExecuteCommand.CanExecute(null));
            await actions.ExecuteCommand.ExecuteAsync(null);
        }
        actions.Attach(Context());
        Assert.False(actions.CanExecute);
        actions.Select(new("selected.txt", " M", false, true), true);
        Assert.False(actions.CanExecute);
        actions.Attach(null);
        Assert.False(actions.HasContext);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TaskSwitchCancelsOldOperationSnapshotsRootsAndRejectsItsResult() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        ToolExecutionContext? captured = null;
        var refreshes = 0;
        var executor = new InspectorProbeToolExecutor { Execute = (_, context, token) =>
        { captured = context; operation = token; return pending.Task; } };
        await using var actions = new GitIndexActionsViewModel(executor, _ => { refreshes++; return Task.CompletedTask; });
        var roots = new List<string> { "D:\\synthetic" };
        actions.Attach(Context() with { AuthorizedRoots = roots });
        actions.Select(new("selected.txt", " M", false, true), false);
        var execution = actions.ExecuteCommand.ExecuteAsync(null);
        try
        {
            Assert.True(actions.IsBusy);
            Assert.False(actions.ExecuteCommand.CanExecute(null));
            roots.Clear();
            Assert.Equal(new[] { "D:\\synthetic" }, captured!.AuthorizedRoots);
            actions.Attach(Context());
            Assert.True(operation.IsCancellationRequested);
            Assert.False(execution.IsCompleted);
        }
        finally { pending.TrySetResult(ToolResult.Success("old result")); }
        await execution;
        Assert.Equal(0, refreshes);
        Assert.Empty(actions.Status);
        Assert.False(actions.IsBusy);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CancellationAndDisposalWaitForTheActualOperationOrRefresh(bool duringRefresh) => _session.DispatchAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        var executor = new InspectorProbeToolExecutor { Execute = async (_, _, token) =>
        {
            operation = token;
            if (!duringRefresh) { entered.SetResult(); await release.Task; }
            return ToolResult.Success("done");
        } };
        await using var actions = new GitIndexActionsViewModel(executor, async _ =>
        { entered.SetResult(); await release.Task; });
        actions.Attach(Context());
        actions.Select(new("selected.txt", " M", false, true), false);
        var execution = actions.ExecuteCommand.ExecuteAsync(null);
        Task? disposal = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            actions.CancelCommand.Execute(null);
            Assert.True(operation.IsCancellationRequested);
            Assert.True(actions.IsBusy);
            Assert.False(actions.CanCancel);
            disposal = actions.DisposeAsync().AsTask();
            Assert.False(execution.IsCompleted);
            Assert.False(disposal.IsCompleted);
            Assert.False(actions.ExecuteCommand.CanExecute(null));
        }
        finally { release.TrySetResult(); }
        await execution;
        if (disposal is not null) await disposal;
        Assert.False(actions.HasContext);
    }, TestContext.Current.CancellationToken);
}
