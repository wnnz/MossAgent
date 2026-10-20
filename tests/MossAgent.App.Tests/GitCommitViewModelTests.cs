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
public sealed class GitCommitViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static ToolResult Preview(int count = 1) => ToolResult.Success("preview", JsonSerializer.Serialize(
        new GitCommitPreview(new string('a', 64), count, new string('b', 40), "refs/heads/fixture"), JsonSerializerOptions.Web));

    [Theory]
    [InlineData("git_state_changed", "GitCommitStateChanged")]
    [InlineData("approval_denied", "DiffIndexDenied")]
    [InlineData("git_failed", "GitCommitFailed")]
    [InlineData("tool_timeout", "GitCommitInterrupted")]
    public Task FailurePreservesMessageRequiresFreshConfirmationAndDoesNotRefresh(string code, string key) => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) => Task.FromResult(request.ToolName == "git.commit_preview"
            ? Preview() : ToolResult.Failure("private-value", code)) };
        await using var model = new GitCommitViewModel(executor, _ => throw new InvalidOperationException("Must not refresh."));
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "retain this message";
        await model.CommitCommand.ExecuteAsync(null);
        Assert.True(model.IsOpen);
        Assert.Equal("retain this message", model.Message);
        Assert.False(model.HasPreview);
        Assert.False(model.CanCommit);
        Assert.Equal(InspectorText.Get(key), model.Status);
        Assert.DoesNotContain("private-value", model.Status);
        await model.RefreshPreviewCommand.ExecuteAsync(null);
        Assert.True(model.CanCommit);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EmptyStagingInvalidMessageReadOnlyAndProjectlessToolsCannotCommit() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(Preview(0)) };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask);
        foreach (var context in new[] { Context() with { ApprovalPolicy = ApprovalPolicy.ReadOnly }, Context() with { ToolsEnabled = false } })
        { model.Attach(context); Assert.False(model.OpenCommand.CanExecute(null)); }
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "valid";
        Assert.False(model.CanCommit);
        executor.Execute = (_, _, _) => Task.FromResult(Preview());
        await model.RefreshPreviewCommand.ExecuteAsync(null);
        Assert.True(model.CanCommit);
        foreach (var message in new[] { " ", "invalid\0value", new string('x', 8193) })
        { model.Message = message; Assert.False(model.CanCommit); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SwitchingTasksCancelsOldCommitRetainsBothDraftsAndRejectsOldSuccess() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken operation = default;
        ToolExecutionContext? captured = null;
        var executor = new InspectorProbeToolExecutor { Execute = (request, context, token) =>
        {
            if (request.ToolName == "git.commit_preview") return Task.FromResult(Preview());
            captured = context; operation = token; return pending.Task;
        } };
        var refreshes = 0;
        await using var model = new GitCommitViewModel(executor, _ => { refreshes++; return Task.CompletedTask; });
        var roots = new List<string> { "D:\\synthetic" };
        var first = Context() with { AuthorizedRoots = roots };
        var second = Context();
        model.Attach(first);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "first draft";
        var execution = model.CommitCommand.ExecuteAsync(null);
        try
        {
            roots.Clear();
            Assert.Single(captured!.AuthorizedRoots);
            Assert.False(model.CloseCommand.CanExecute(null));
            model.Attach(second);
            model.Message = "second draft";
            Assert.True(operation.IsCancellationRequested);
            Assert.False(execution.IsCompleted);
        }
        finally { pending.TrySetResult(ToolResult.Success("old commit")); }
        await execution;
        Assert.Equal("second draft", model.Message);
        Assert.Empty(model.Status);
        Assert.Equal(0, refreshes);
        model.Attach(first);
        Assert.Equal("first draft", model.Message);
        model.Attach(second);
        Assert.Equal("second draft", model.Message);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SuccessfulCommitReleasesBusyStateBeforeClosingTheDialog(bool delayedCommit, bool delayedRefresh) => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
            request.ToolName == "git.commit_preview" ? Task.FromResult(Preview())
                : delayedCommit ? pending.Task : Task.FromResult(ToolResult.Success("committed")) };
        await using var model = new GitCommitViewModel(executor, _ =>
        {
            refreshing.TrySetResult();
            return delayedRefresh ? release.Task : Task.CompletedTask;
        });
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "commit this message";
        bool? busyAtClose = null;
        model.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(model.IsOpen) && !model.IsOpen) busyAtClose = model.IsBusy;
        };
        var execution = model.CommitCommand.ExecuteAsync(null);
        try
        {
            pending.TrySetResult(ToolResult.Success("committed"));
            await refreshing.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (delayedRefresh)
            {
                Assert.True(model.IsBusy);
                Assert.True(model.IsOpen);
                Assert.False(model.OpenCommand.CanExecute(null));
            }
        }
        finally { pending.TrySetResult(ToolResult.Success("cleanup")); release.TrySetResult(); }
        await execution;
        Assert.Equal(false, busyAtClose);
        Assert.False(model.IsOpen);
        Assert.False(model.IsBusy);
        Assert.True(model.OpenCommand.CanExecute(null));
        Assert.Empty(model.Message);
        Assert.Equal(InspectorText.Get("GitCommitSucceeded"), model.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisposeWaitsForActualCommitOrRefreshCleanup(bool duringRefresh) => _session.DispatchAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = async (request, _, _) =>
        {
            if (request.ToolName == "git.commit_preview") return Preview();
            if (!duringRefresh) { entered.SetResult(); await release.Task; }
            return ToolResult.Success("committed");
        } };
        await using var model = new GitCommitViewModel(executor, async _ => { entered.SetResult(); await release.Task; });
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "preserve until cleanup";
        var execution = model.CommitCommand.ExecuteAsync(null);
        Task? disposal = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            model.CancelCommand.Execute(null);
            Assert.True(model.IsBusy);
            disposal = model.DisposeAsync().AsTask();
            Assert.False(execution.IsCompleted);
            Assert.False(disposal.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await execution;
        if (disposal is not null) await disposal;
        Assert.False(model.HasContext);
        Assert.False(model.CommitCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
