using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitPushLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static ToolResult Preview() => ToolResult.Success("preview", JsonSerializer.Serialize(new GitPushPreview(
        new string('a', 64), new string('b', 40), "fixture", "origin", "fixture", "local", ["origin"], null), JsonSerializerOptions.Web));

    [Fact]
    public Task DelayedPreviewCannotReplaceEditedSelection() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        var execution = model.OpenCommand.ExecuteAsync(null);
        try { model.Remote = "upstream"; model.TargetBranch = "new-target"; }
        finally { pending.TrySetResult(Preview()); }
        await execution;
        Assert.Equal("upstream", model.Remote);
        Assert.Equal("new-target", model.TargetBranch);
        Assert.False(model.HasPreview);
        Assert.False(model.CanPush);
        Assert.Equal(InspectorText.Get("GitPushSelectionChanged"), model.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SwitchingTasksCancelsOldPushRetainsSelectionsAndRejectsOldSuccess() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        ToolExecutionContext? captured = null;
        var executor = new InspectorProbeToolExecutor { Execute = (request, context, token) =>
        {
            if (request.ToolName == "git.push_preview") return Task.FromResult(Preview());
            captured = context; operation = token; return pending.Task;
        } };
        var refreshes = 0;
        await using var model = new GitPushViewModel(executor, _ => { refreshes++; return Task.CompletedTask; });
        var roots = new List<string> { "D:\\synthetic" };
        var first = Context() with { AuthorizedRoots = roots };
        var second = Context();
        model.Attach(first);
        await model.OpenCommand.ExecuteAsync(null);
        var execution = model.PushCommand.ExecuteAsync(null);
        try
        {
            roots.Clear();
            Assert.Single(captured!.AuthorizedRoots);
            model.Attach(second);
            model.Remote = "other";
            model.TargetBranch = "second-task";
            Assert.True(operation.IsCancellationRequested);
            Assert.False(execution.IsCompleted);
            Assert.False(model.OpenCommand.CanExecute(null));
        }
        finally { pending.TrySetResult(ToolResult.Success("old push")); }
        await execution;
        Assert.Equal("other", model.Remote);
        Assert.Equal("second-task", model.TargetBranch);
        Assert.Empty(model.Status);
        Assert.Equal(0, refreshes);
        model.Attach(first);
        Assert.Equal("origin", model.Remote);
        Assert.Equal("fixture", model.TargetBranch);
        model.Attach(second);
        Assert.Equal("other", model.Remote);
        Assert.Equal("second-task", model.TargetBranch);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SuccessfulPushReleasesBusyBeforeClosingAndWaitsForRefresh(bool delayedPush, bool delayedRefresh) => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) => request.ToolName == "git.push_preview" ? Task.FromResult(Preview())
            : delayedPush ? pending.Task : Task.FromResult(ToolResult.Success("pushed")) };
        await using var model = new GitPushViewModel(executor, _ => { refreshing.TrySetResult(); return delayedRefresh ? release.Task : Task.CompletedTask; });
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        bool? busyAtClose = null;
        model.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(model.IsOpen) && !model.IsOpen) busyAtClose = model.IsBusy; };
        var execution = model.PushCommand.ExecuteAsync(null);
        try
        {
            pending.TrySetResult(ToolResult.Success("pushed"));
            await refreshing.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (delayedRefresh) { Assert.True(model.IsBusy); Assert.True(model.IsOpen); Assert.False(execution.IsCompleted); }
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); release.TrySetResult(); }
        await execution;
        Assert.Equal(false, busyAtClose);
        Assert.False(model.IsBusy);
        Assert.False(model.IsOpen);
        Assert.True(model.OpenCommand.CanExecute(null));
        Assert.Equal(InspectorText.Get("GitPushSucceeded"), model.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CancelAndDisposeWaitForActualPushOrRefreshCleanup(bool duringRefresh) => _session.DispatchAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        var executor = new InspectorProbeToolExecutor { Execute = async (request, _, token) =>
        {
            if (request.ToolName == "git.push_preview") return Preview();
            operation = token;
            if (!duringRefresh) { entered.TrySetResult(); await release.Task; }
            return ToolResult.Success("pushed");
        } };
        await using var model = new GitPushViewModel(executor, async _ => { entered.TrySetResult(); await release.Task; });
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        var execution = model.PushCommand.ExecuteAsync(null);
        Task? disposal = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            model.CancelCommand.Execute(null);
            Assert.True(operation.IsCancellationRequested);
            Assert.True(model.IsBusy);
            Assert.False(model.CloseCommand.CanExecute(null));
            Assert.False(model.CanCancel);
            disposal = model.DisposeAsync().AsTask();
            Assert.False(execution.IsCompleted);
            Assert.False(disposal.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await execution;
        if (disposal is not null) await disposal;
        Assert.False(model.HasContext);
        Assert.False(model.PushCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
