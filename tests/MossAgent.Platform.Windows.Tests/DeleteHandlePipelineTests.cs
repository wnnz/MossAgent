using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DeleteHandlePipelineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("readonly", "capability_denied")]
    [InlineData("denied", "approval_denied")]
    [InlineData("no-roots", "path_denied")]
    [InlineData("invalid", "invalid_arguments")]
    public async Task RefusedRequestsNeverCreateSessionOrDelete(string scenario, string error)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.FileFor(fixture.Root);
        var sessions = new CallbackPathDeleteSessions();
        var executor = Executor(sessions, approved: false);
        var context = Context(fixture.Root) with
        {
            ApprovalPolicy = scenario == "readonly" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime,
            AuthorizedRoots = scenario == "no-roots" ? [] : [fixture.Root]
        };
        var request = scenario == "invalid" ? Request(new { path, recursive = "yes" }) : Request(new { path });
        var result = await executor.ExecuteAsync(request, context, Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(0, sessions.Creates);
        Assert.True(File.Exists(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDeadlineAfterPrepareLeavesFileAndReleasesAllHandles(bool deadline)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.FileFor(fixture.Root);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var time = new ManualToolTimeProvider();
        var sessions = new CallbackPathDeleteSessions { Prepared = _ =>
        {
            if (deadline) time.Advance(TimeSpan.FromSeconds(2));
            else cancellation.Cancel();
        } };
        var result = await Executor(sessions, time: time).ExecuteAsync(Request(new { path }),
            Context(fixture.Root) with { Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.Equal(0, sessions.Last!.DeletedCount);
        Assert.False(sessions.Last.FileContent!.CanRead);
        await File.WriteAllTextAsync(path, "handle released", Token);
    }

    [Theory]
    [InlineData("cancel", "tool_cancelled")]
    [InlineData("deadline", "tool_timeout")]
    [InlineData("revoke", "file_delete_incomplete")]
    [InlineData("new-child", "file_delete_incomplete")]
    public async Task PartialDeletionIsVisibleAndInvalidatesSnapshots(string scenario, string error)
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.DirectoryFor("tree");
        var child = fixture.FileFor(path);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var time = new ManualToolTimeProvider();
        var roots = new List<string> { fixture.Root };
        var context = Context(fixture.Root) with { AuthorizedRoots = roots, Timeout = TimeSpan.FromSeconds(1) };
        var snapshot = await FileSnapshotReader.ReadAsync(child, Token, context);
        context.FileSnapshots.Record(context.TaskId, FileSnapshotReader.CacheKey(child), snapshot);
        var sessions = new CallbackPathDeleteSessions { Validate = (session, _) =>
        {
            if (session.DeletedCount == 1)
            {
                if (scenario == "cancel") cancellation.Cancel();
                if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
                if (scenario == "revoke") roots.Clear();
                if (scenario == "new-child") fixture.FileFor(path, "new.txt");
            }
            return Task.CompletedTask;
        } };
        var result = await Executor(sessions, time: time).ExecuteAsync(Request(new { path, recursive = true }), context, cancellation.Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.Contains("1/2", result.Summary);
        Assert.Contains("人工核实", result.Summary);
        Assert.Equal(1, sessions.Last!.DeletedCount);
        Assert.Null(context.FileSnapshots.Get(context.TaskId, FileSnapshotReader.CacheKey(child)));
        Assert.True(Directory.Exists(path));
        Assert.False(File.Exists(child));
        if (scenario == "new-child") Assert.True(File.Exists(Path.Combine(path, "new.txt")));
    }

    [Fact]
    public async Task CancellationOfOnePreparedTaskDoesNotCancelAnotherTask()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var first = fixture.FileFor(fixture.Root, "first.txt");
        var second = fixture.FileFor(fixture.Root, "second.txt");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessions = new CallbackPathDeleteSessions { Validate = async (_, token) =>
        {
            prepared.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        } };
        var firstRun = Executor(sessions).ExecuteAsync(Request(new { path = first }), Context(fixture.Root), cancellation.Token);
        await prepared.Task.WaitAsync(Token);
        var secondResult = await Executor(new()).ExecuteAsync(Request(new { path = second }), Context(fixture.Root), Token);
        cancellation.Cancel();
        var firstResult = await firstRun.WaitAsync(Token);
        Assert.True(secondResult.IsSuccess, secondResult.Summary);
        Assert.Equal("tool_cancelled", firstResult.ErrorCode);
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
        Assert.False(sessions.Last!.FileContent!.CanRead);
    }

    [Fact]
    public async Task SuccessfulDeletionStillObeysOutputLimit()
    {
        using var fixture = new ReadHandleDirectoryFixture();
        var path = fixture.FileFor(fixture.Root);
        var result = await Executor(new()).ExecuteAsync(Request(new { path }),
            Context(fixture.Root) with { MaximumOutputCharacters = 24 }, Token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(result.OutputCharacterCount <= 24);
        Assert.False(File.Exists(path));
    }

    private static ToolExecutor Executor(CallbackPathDeleteSessions sessions, bool approved = true, TimeProvider? time = null) =>
        new(new([new PathDeleteTool(new(), sessions)]),
            new PolicyApprovalService((_, _, _) => ValueTask.FromResult(approved)), timeProvider: time);

    private static ToolExecutionContext Context(string root) => new(Guid.NewGuid(), root, [root], ApprovalPolicy.AskEveryTime);
    private static ToolRequest Request(object arguments) => new(Guid.NewGuid().ToString("N"), "filesystem.delete_path", JsonSerializer.SerializeToElement(arguments));
}
