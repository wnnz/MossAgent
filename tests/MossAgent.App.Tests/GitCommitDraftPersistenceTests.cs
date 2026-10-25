using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitCommitDraftPersistenceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    private readonly CancellationToken _token = TestContext.Current.CancellationToken;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(20);
    private const string Head = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string NewHead = "cccccccccccccccccccccccccccccccccccccccc";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static ToolExecutionContext Context() =>
        new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);

    private static ToolResult Preview(int staged, string head) => ToolResult.Success("preview", JsonSerializer.Serialize(
        new GitCommitPreview(new string('a', 64), staged, head, "refs/heads/fixture"), JsonSerializerOptions.Web));

    [Fact]
    public Task PersistedDraftIsRestoredOnAttachAndSurvivesAcrossViewModelInstances() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var repository = new TestGitCommitDraftRepository();
        repository.Seed(context.TaskId, "重启前未发送的提交说明", 3);
        var executor = new InspectorProbeToolExecutor();
        GitCommitDraft? stored = await repository.GetAsync(context.TaskId, _token);
        Assert.Equal(3, stored!.Revision);
        await using (var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay))
        {
            model.Attach(context);
            await model.DraftLoad.WaitAsync(Timeout, _token);
            Assert.Equal("重启前未发送的提交说明", model.Message);
            var other = Context();
            model.Attach(other);
            await model.DraftLoad.WaitAsync(Timeout, _token);
            Assert.Equal(string.Empty, model.Message);
            model.Attach(context);
            await model.DraftLoad.WaitAsync(Timeout, _token);
            Assert.Equal("重启前未发送的提交说明", model.Message);
        }
        await using var reopened = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        reopened.Attach(context);
        await reopened.DraftLoad.WaitAsync(Timeout, _token);
        Assert.Equal("重启前未发送的提交说明", reopened.Message);
    }, _token);

    [Fact]
    public Task InFlightRestoreDoesNotOverrideUserEditsAndKeepsTheirRevision() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<GitCommitDraft?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestGitCommitDraftRepository { ReadHandler = (_, cancellationToken) =>
        {
            entered.TrySetResult();
            return release.Task.WaitAsync(cancellationToken);
        } };
        repository.Seed(context.TaskId, "stored draft", 4);
        await using var model = new GitCommitViewModel(new InspectorProbeToolExecutor(), _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await entered.Task.WaitAsync(Timeout, _token);
        model.Message = "user typed meanwhile";
        release.TrySetResult(new GitCommitDraft("stored draft", 4));
        await model.DraftLoad.WaitAsync(Timeout, _token);
        Assert.Equal("user typed meanwhile", model.Message);
        repository.ReadHandler = null;
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("user typed meanwhile", 5), await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Fact]
    public Task MessageEditsPersistDebouncedAndEmptyTextDeletesTheRow() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var repository = new TestGitCommitDraftRepository();
        await using var model = new GitCommitViewModel(new InspectorProbeToolExecutor(), _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.DraftLoad.WaitAsync(Timeout, _token);
        model.Message = "draft one";
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("draft one", 1), await repository.GetAsync(context.TaskId, _token));
        model.Message = "draft two";
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("draft two", 2), await repository.GetAsync(context.TaskId, _token));
        model.Message = string.Empty;
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Null(await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Fact]
    public Task DraftsStaySeparateWhenSwitchingTasks() => _session.DispatchAsync(async () =>
    {
        var first = Context();
        var second = Context();
        var repository = new TestGitCommitDraftRepository();
        await using var model = new GitCommitViewModel(new InspectorProbeToolExecutor(), _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(first);
        await model.DraftLoad.WaitAsync(Timeout, _token);
        model.Message = "first draft";
        model.Attach(second);
        await model.DraftLoad.WaitAsync(Timeout, _token);
        Assert.Equal(string.Empty, model.Message);
        model.Message = "second draft";
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("first draft", 1), await repository.GetAsync(first.TaskId, _token));
        Assert.Equal(new GitCommitDraft("second draft", 1), await repository.GetAsync(second.TaskId, _token));
        model.Attach(first);
        Assert.Equal("first draft", model.Message);
        model.Attach(second);
        Assert.Equal("second draft", model.Message);
    }, _token);

    [Fact]
    public Task SuccessfulCommitConsumesThePersistedDraft() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var repository = new TestGitCommitDraftRepository();
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
            Task.FromResult(request.ToolName == "git.commit" ? ToolResult.Success("committed") : Preview(1, Head)) };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "commit this message";
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.NotNull(await repository.GetAsync(context.TaskId, _token));
        await model.CommitCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, model.Message);
        Assert.Equal(InspectorText.Get("GitCommitSucceeded"), model.Status);
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Null(await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Theory]
    [InlineData("git_state_changed")]
    [InlineData("git_nothing_staged")]
    [InlineData("approval_denied")]
    [InlineData("invalid_arguments")]
    public Task DeterministicFailuresKeepTheDraftAndSkipVerification(string errorCode) => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var previews = 0;
        var repository = new TestGitCommitDraftRepository();
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.commit_preview") previews++;
            return Task.FromResult(request.ToolName == "git.commit"
                ? ToolResult.Failure("nope", errorCode) : Preview(1, Head));
        } };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "kept draft";
        await model.CommitCommand.ExecuteAsync(null);
        Assert.Equal(1, previews);
        Assert.Equal("kept draft", model.Message);
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("kept draft", 1), await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Fact]
    public Task TimeoutWithAdvancedHeadAndEmptyIndexConsumesTheDraft() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var attempted = false;
        var repository = new TestGitCommitDraftRepository();
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.commit")
            {
                attempted = true;
                return Task.FromResult(ToolResult.Failure("timed out", "tool_timeout"));
            }
            return Task.FromResult(attempted ? Preview(0, NewHead) : Preview(1, Head));
        } };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "committed while timing out";
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.NotNull(await repository.GetAsync(context.TaskId, _token));
        await model.CommitCommand.ExecuteAsync(null);
        Assert.Equal(InspectorText.Get("GitCommitProbablyLanded"), model.Status);
        Assert.Equal(string.Empty, model.Message);
        Assert.True(model.IsOpen);
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Null(await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Fact]
    public Task TimeoutWithUnchangedHeadKeepsTheDraft() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var previews = 0;
        var repository = new TestGitCommitDraftRepository();
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.commit_preview") previews++;
            return Task.FromResult(request.ToolName == "git.commit"
                ? ToolResult.Failure("timed out", "tool_timeout") : Preview(1, Head));
        } };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "kept after timeout";
        await model.CommitCommand.ExecuteAsync(null);
        Assert.Equal(2, previews);
        Assert.Equal(InspectorText.Get("GitCommitInterrupted"), model.Status);
        Assert.Equal("kept after timeout", model.Message);
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("kept after timeout", 1), await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Fact]
    public Task TimeoutWithAdvancedHeadAndRemainingStagedFilesKeepsTheDraft() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var attempted = false;
        var repository = new TestGitCommitDraftRepository();
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.commit")
            {
                attempted = true;
                return Task.FromResult(ToolResult.Failure("cancelled", "tool_cancelled"));
            }
            return Task.FromResult(attempted ? Preview(2, NewHead) : Preview(1, Head));
        } };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "kept after mixed state";
        await model.CommitCommand.ExecuteAsync(null);
        Assert.Equal(InspectorText.Get("GitCommitExternallyChanged"), model.Status);
        Assert.Equal("kept after mixed state", model.Message);
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("kept after mixed state", 1), await repository.GetAsync(context.TaskId, _token));
    }, _token);

    [Fact]
    public Task FailedVerificationKeepsTheDraftWithAnExplicitStatus() => _session.DispatchAsync(async () =>
    {
        var context = Context();
        var attempted = false;
        var repository = new TestGitCommitDraftRepository();
        var executor = new InspectorProbeToolExecutor { Execute = (request, _, _) =>
        {
            if (request.ToolName == "git.commit")
            {
                attempted = true;
                return Task.FromResult(ToolResult.Failure("failed", "git_failed"));
            }
            return Task.FromResult(attempted
                ? ToolResult.Failure("cannot verify", "git_preview_incomplete")
                : Preview(1, Head));
        } };
        await using var model = new GitCommitViewModel(executor, _ => Task.CompletedTask, repository, SaveDelay);
        model.Attach(context);
        await model.OpenCommand.ExecuteAsync(null);
        model.Message = "kept after failed verification";
        await model.CommitCommand.ExecuteAsync(null);
        Assert.Equal(InspectorText.Get("GitCommitOutcomeUnknown"), model.Status);
        Assert.Equal("kept after failed verification", model.Message);
        await model.FlushDraftsAsync().WaitAsync(Timeout, _token);
        Assert.Equal(new GitCommitDraft("kept after failed verification", 1), await repository.GetAsync(context.TaskId, _token));
    }, _token);
}
