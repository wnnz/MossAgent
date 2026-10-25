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
public sealed class GitPushOutcomeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
    private static ToolResult Preview(string? remoteHead = null) => ToolResult.Success("preview",
        JsonSerializer.Serialize(new GitPushPreview(new string('a', 64), new string('b', 40), "feature/source", "origin",
            "feature/review", "https://example.invalid", ["origin", "upstream"], remoteHead), JsonSerializerOptions.Web));

    [Theory]
    [InlineData("git_state_changed")]
    [InlineData("approval_denied")]
    public Task DeterministicPushFailuresSkipTheOutcomeVerification(string code) => _session.DispatchAsync(async () =>
    {
        var previews = 0;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.push_preview") previews++;
            return Task.FromResult(request.ToolName == "git.push" ? ToolResult.Failure("private-value", code) : Preview());
        } };
        await using var model = new GitPushViewModel(executor, _ => throw new InvalidOperationException("Must not refresh."));
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        await model.PushCommand.ExecuteAsync(null);
        Assert.Equal(1, previews);
        Assert.Equal(InspectorText.Get(code == "git_state_changed" ? "GitPushStateChanged" : "DiffIndexDenied"), model.Status);
        Assert.DoesNotContain("private-value", model.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TimeoutWithRemoteHeadMatchingThePushedCommitReportsProbablyLanded() => _session.DispatchAsync(async () =>
    {
        var attempted = false;
        JsonElement verification = default;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.push")
            {
                attempted = true;
                return Task.FromResult(ToolResult.Failure("timed out", "tool_timeout"));
            }
            if (attempted) verification = request.Arguments;
            return Task.FromResult(Preview(attempted ? new string('b', 40) : null));
        } };
        await using var model = new GitPushViewModel(executor, _ => throw new InvalidOperationException("Must not refresh."));
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        await model.PushCommand.ExecuteAsync(null);
        Assert.Equal("origin", verification.GetProperty("remote").GetString());
        Assert.Equal("feature/review", verification.GetProperty("targetBranch").GetString());
        Assert.True(model.IsOpen);
        Assert.False(model.HasPreview);
        Assert.False(model.CanPush);
        Assert.Equal("origin", model.Remote);
        Assert.Equal("feature/review", model.TargetBranch);
        Assert.Equal(InspectorText.Get("GitPushProbablyLanded"), model.Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(null)]
    [InlineData("cccccccccccccccccccccccccccccccccccccccc")]
    public Task TimeoutWithADifferentOrMissingRemoteHeadKeepsTheOriginalFailure(string? remoteHead) => _session.DispatchAsync(async () =>
    {
        var attempted = false;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.push")
            {
                attempted = true;
                return Task.FromResult(ToolResult.Failure("timed out", "tool_timeout"));
            }
            return Task.FromResult(Preview(attempted ? remoteHead : null));
        } };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        await model.PushCommand.ExecuteAsync(null);
        Assert.True(model.IsOpen);
        Assert.Equal(InspectorText.Get("GitPushInterrupted"), model.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedVerificationReportsAnExplicitUnknownOutcome() => _session.DispatchAsync(async () =>
    {
        var attempted = false;
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.push")
            {
                attempted = true;
                return Task.FromResult(ToolResult.Failure("private-value", "git_push_failed"));
            }
            return Task.FromResult(attempted ? ToolResult.Failure("cannot verify", "git_push_preview_incomplete") : Preview());
        } };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        await model.PushCommand.ExecuteAsync(null);
        Assert.True(model.IsOpen);
        Assert.Equal(InspectorText.Get("GitPushOutcomeUnknown"), model.Status);
        Assert.DoesNotContain("private-value", model.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancellationSkipsTheOutcomeVerification() => _session.DispatchAsync(async () =>
    {
        var previews = 0;
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.push_preview") { previews++; return Task.FromResult(Preview()); }
            return pending.Task;
        } };
        await using var model = new GitPushViewModel(executor, _ => Task.CompletedTask);
        model.Attach(Context());
        await model.OpenCommand.ExecuteAsync(null);
        var push = model.PushCommand.ExecuteAsync(null);
        model.CancelCommand.Execute(null);
        pending.TrySetResult(ToolResult.Failure("cancelled", "tool_cancelled"));
        await push;
        Assert.Equal(1, previews);
        Assert.True(model.IsOpen);
        Assert.Equal(InspectorText.Get("GitPushInterrupted"), model.Status);
    }, TestContext.Current.CancellationToken);
}
