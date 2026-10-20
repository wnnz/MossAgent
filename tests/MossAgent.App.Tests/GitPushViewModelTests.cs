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
public sealed class GitPushViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static ToolResult Preview(string remote = "origin", string branch = "feature/review") => ToolResult.Success("preview",
        JsonSerializer.Serialize(new GitPushPreview(new string('a', 64), new string('b', 40), "feature/source", remote,
            branch, "https://example.invalid", ["origin", "upstream"]), JsonSerializerOptions.Web));

    [Theory]
    [InlineData("git_state_changed", "GitPushStateChanged")]
    [InlineData("git_push_no_remote", "GitPushNoRemote")]
    [InlineData("git_push_no_head", "GitPushNoHead")]
    [InlineData("git_push_branch", "GitPushBranchRequired")]
    [InlineData("git_push_transport", "GitPushTransportUnsupported")]
    [InlineData("git_push_multiple_urls", "GitPushMultipleUrls")]
    [InlineData("approval_denied", "DiffIndexDenied")]
    [InlineData("capability_denied", "DiffIndexDenied")]
    [InlineData("path_denied", "DiffIndexPathDenied")]
    [InlineData("workspace_denied", "DiffIndexPathDenied")]
    [InlineData("git_push_failed", "GitPushFailed")]
    [InlineData("tool_cancelled", "GitPushInterrupted")]
    [InlineData("tool_timeout", "GitPushInterrupted")]
    public Task FailurePreservesSelectionRequiresFreshConfirmationAndSuppressesRawOutput(string code, string key) => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) => Task.FromResult(request.ToolName == "git.push_preview"
            ? Preview() : ToolResult.Failure("private-value", code)) };
        await using var model = new GitPushViewModel(executor, _ => throw new InvalidOperationException("Must not refresh."));
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        Assert.True(model.CanPush);
        Assert.Contains("origin", model.AvailableRemotes);
        Assert.Contains(new string('b', 12), model.PreviewSummary);
        await model.PushCommand.ExecuteAsync(null);
        Assert.True(model.IsOpen);
        Assert.Equal("origin", model.Remote);
        Assert.Equal("feature/review", model.TargetBranch);
        Assert.False(model.HasPreview);
        Assert.False(model.CanPush);
        Assert.Equal(InspectorText.Get(key), model.Status);
        Assert.DoesNotContain("private-value", model.Status);
        await model.RefreshPreviewCommand.ExecuteAsync(null);
        Assert.True(model.CanPush);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EditingEitherSelectionInvalidatesTheExactConfirmationAndChangesArguments() => _session.DispatchAsync(async () =>
    {
        ToolRequest? pushed = null;
        var refreshes = 0;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.push") { pushed = request; return Task.FromResult(ToolResult.Success("pushed")); }
            return Task.FromResult(Preview(request.Arguments.TryGetProperty("remote", out var remote) ? remote.GetString()! : "origin",
                request.Arguments.TryGetProperty("targetBranch", out var branch) ? branch.GetString()! : "feature/review"));
        } };
        await using var model = new GitPushViewModel(executor, _ => { refreshes++; return Task.CompletedTask; });
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        model.Remote = "upstream";
        Assert.False(model.CanPush);
        await model.PushCommand.ExecuteAsync(null);
        Assert.Null(pushed);
        await model.RefreshPreviewCommand.ExecuteAsync(null);
        model.TargetBranch = "review/explicit";
        Assert.False(model.CanPush);
        await model.RefreshPreviewCommand.ExecuteAsync(null);
        await model.PushCommand.ExecuteAsync(null);
        Assert.Equal("upstream", pushed!.Arguments.GetProperty("remote").GetString());
        Assert.Equal("review/explicit", pushed.Arguments.GetProperty("targetBranch").GetString());
        Assert.Equal(new string('a', 64), pushed.Arguments.GetProperty("expectedState").GetString());
        Assert.False(model.IsOpen);
        Assert.Equal(1, refreshes);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReadOnlyDisabledAndMissingContextsDoNotExecuteTools() => _session.DispatchAsync(async () =>
    {
        var calls = 0;
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => { calls++; return Task.FromResult(Preview()); } };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        foreach (var context in new[] { null, Context() with { ApprovalPolicy = ApprovalPolicy.ReadOnly }, Context() with { ToolsEnabled = false } })
        {
            model.Attach(context);
            Assert.False(model.OpenCommand.CanExecute(null));
            await model.OpenCommand.ExecuteAsync(null);
            await model.PushCommand.ExecuteAsync(null);
            Assert.False(model.IsOpen);
        }
        Assert.Equal(0, calls);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("null")]
    [InlineData("invalid json")]
    [InlineData("{}")]
    public Task InvalidPreviewNeverEnablesPushOrExposesTheContent(string content) => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(ToolResult.Success("private-value", content)) };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        Assert.True(model.IsOpen);
        Assert.False(model.CanPush);
        Assert.False(model.HasPreview);
        Assert.False(model.IsBusy);
        Assert.Equal(InspectorText.Get("GitPushFailed"), model.Status);
    }, TestContext.Current.CancellationToken);
}
