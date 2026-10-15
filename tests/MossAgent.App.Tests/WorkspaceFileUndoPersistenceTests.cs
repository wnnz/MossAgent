using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFileUndoPersistenceTests
{
    [Theory]
    [InlineData("tool_cancelled")]
    [InlineData("tool_timeout")]
    public async Task LateCommittedFailure_IsSavedWithAFreshBoundedTokenBeforeStopping(string error)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var plan = Plan(2);
        var inverse = plan.Changes[0] with { BeforeSha256 = plan.Changes[0].AfterSha256, AfterSha256 = plan.Changes[0].BeforeSha256 };
        var executor = new UndoResultToolExecutor((_, _, _) =>
        {
            caller.Cancel();
            return Task.FromResult(ToolResult.Failure("late committed", error) with { FileChanges = [inverse] });
        });
        var repository = new TestWorkspaceRepository();
        repository.AppendMessageHandler = (message, token) =>
        {
            Assert.True(token.CanBeCanceled);
            Assert.False(token.IsCancellationRequested);
            repository.Messages.Add(message);
            return Task.CompletedTask;
        };
        var published = new List<ConversationMessage>();
        var outcome = await new WorkspaceFileChangeUndoService(executor, repository).ExecuteAsync(plan,
            _ => Task.FromResult(true), message => { published.Add(message); return Task.CompletedTask; }, caller.Token);

        Assert.Equal("FileUndoStopped", outcome.StatusKey);
        var saved = Assert.Single(repository.Messages);
        Assert.Equal(saved, Assert.Single(published));
        Assert.Equal(inverse, Assert.Single(WorkspaceConversationMapper.ToChatMessage(saved).Tool!.FileChanges!));
        Assert.Equal(1, executor.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrUncertainPersistence_RetryOnlySavesTheSameHistoryRecord(bool committedBeforeFailure)
    {
        var plan = Plan(2);
        var executor = new UndoResultToolExecutor((_, _, _) =>
            Task.FromResult(ToolResult.Success("restored") with { FileChanges = [plan.Changes[0]] }));
        var repository = new TestWorkspaceRepository();
        repository.AppendMessageHandler = (message, _) =>
        {
            if (committedBeforeFailure) repository.Messages.Add(message);
            throw new IOException("synthetic-private-diagnostic");
        };
        var service = new WorkspaceFileChangeUndoService(executor, repository);
        var published = new List<ConversationMessage>();
        var outcome = await service.ExecuteAsync(plan, _ => Task.FromResult(true),
            message => { published.Add(message); return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        Assert.Equal("FileUndoSaveFailed", outcome.StatusKey);
        Assert.Null(outcome.Detail);
        var pending = Assert.IsType<ConversationMessage>(outcome.UnsavedRecord);
        Assert.Equal(pending, Assert.Single(published));
        repository.AppendMessageHandler = null;
        Assert.True(await service.SaveAsync(pending));
        Assert.True(await service.SaveAsync(pending));
        Assert.Equal(pending, Assert.Single(repository.Messages));
        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task HistoryIdentityCollision_IsNotOverwrittenOrDuplicated()
    {
        var plan = Plan(1);
        var pending = new ConversationMessage(Guid.NewGuid(), plan.Task.Id, MessageRole.Tool, "pending", DateTimeOffset.UtcNow);
        var conflicting = pending with { Content = "existing sentinel" };
        var repository = new TestWorkspaceRepository();
        repository.Messages.Add(conflicting);
        var executor = new UndoResultToolExecutor((_, _, _) => Task.FromResult(ToolResult.Success("unused")));

        Assert.False(await new WorkspaceFileChangeUndoService(executor, repository).SaveAsync(pending));

        Assert.Equal(conflicting, Assert.Single(repository.Messages));
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task ScopeInvalidation_StopsAfterPersistingAnAlreadyCommittedPrefix()
    {
        var plan = Plan(2);
        var executor = new UndoResultToolExecutor((_, _, _) => Task.FromResult(ToolResult.Success("restored")));
        var repository = new TestWorkspaceRepository();
        var checks = 0;

        var outcome = await new WorkspaceFileChangeUndoService(executor, repository).ExecuteAsync(plan,
            _ => Task.FromResult(++checks == 1), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Equal("FileUndoStopped", outcome.StatusKey);
        Assert.Single(repository.Messages);
        Assert.Equal(1, executor.Calls);
    }

    [Theory]
    [InlineData(ApprovalPolicy.FullAccess, ApprovalPolicy.AskEveryTime)]
    [InlineData(ApprovalPolicy.ReadOnly, ApprovalPolicy.ReadOnly)]
    public async Task CreationRemoval_NeverRelaxesReadOnlyAndTightensFullAccess(ApprovalPolicy policy, ApprovalPolicy expected)
    {
        var plan = Plan(1);
        plan = plan with { Context = plan.Context with { ApprovalPolicy = policy }, Changes = [plan.Changes[0] with { BeforeSha256 = null }] };
        var executor = new UndoResultToolExecutor((request, context, _) =>
        {
            Assert.Equal("filesystem.remove_created_file", request.ToolName);
            Assert.Equal(expected, context.ApprovalPolicy);
            Assert.Equal(64 * 1024, context.MaximumOutputCharacters);
            Assert.Equal(plan.Changes[0], context.ReviewedChange);
            return Task.FromResult(ToolResult.Failure("denied", "approval_denied"));
        });

        await new WorkspaceFileChangeUndoService(executor, new TestWorkspaceRepository()).ExecuteAsync(plan,
            _ => Task.FromResult(true), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Equal(1, executor.Calls);
    }

    [Fact]
    public async Task CrossTaskPlan_IsRejectedBeforeExecutionOrPersistence()
    {
        var plan = Plan(1);
        plan = plan with { Context = plan.Context with { TaskId = Guid.NewGuid() } };
        var executor = new UndoResultToolExecutor((_, _, _) => Task.FromResult(ToolResult.Success("unused")));
        var repository = new TestWorkspaceRepository();

        var outcome = await new WorkspaceFileChangeUndoService(executor, repository).ExecuteAsync(plan,
            _ => Task.FromResult(true), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Equal("FileUndoFailed", outcome.StatusKey);
        Assert.Equal(0, executor.Calls);
        Assert.Empty(repository.Messages);
    }

    private static WorkspaceFileUndoPlan Plan(int count)
    {
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
        var task = new AgentTask(Guid.NewGuid(), project.Id, "fixture", ApprovalPolicy.FullAccess,
            AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var context = new ToolExecutionContext(task.Id, project.PrimaryDirectory, project.AuthorizedDirectories, task.ApprovalPolicy);
        var changes = Enumerable.Range(0, count).Select(index => new ToolFileChange($"file-{index}.txt", new string('A', 64), new string('B', 64))).ToArray();
        return new(task, project, context, changes);
    }
}
