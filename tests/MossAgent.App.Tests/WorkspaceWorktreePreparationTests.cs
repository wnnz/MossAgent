using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreePreparationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitChoice_IsPersistedAndDoesNotRaiseApproval(bool worktree)
    {
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        var task = await factory.PrepareAsync(CreateProject(), null, "prompt", ApprovalPolicy.ReadOnly, [], Token, worktree);
        Assert.Equal(worktree ? 1 : 0, probe.Created.Count);
        Assert.Equal(worktree, task.WorktreePath is not null);
        Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        Assert.Equal(task, repository.SavedTask);
        Assert.Equal(task.Id, Assert.Single(repository.Messages).TaskId);
        Assert.Empty(probe.Removed);
    }

    [Fact]
    public async Task ProjectlessWorktree_IsRejectedBeforeCreatingAnything()
    {
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.PrepareAsync(null, null, "prompt",
            ApprovalPolicy.ReadOnly, [], Token, useWorktree: true));
        Assert.Empty(probe.Created);
        Assert.Null(repository.SavedTask);
    }

    [Fact]
    public async Task ExistingConversation_CannotChangeEnvironment()
    {
        var project = CreateProject();
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        var task = await factory.PrepareAsync(project, null, "first", ApprovalPolicy.ReadOnly, [], Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.PrepareAsync(project, task, "next",
            ApprovalPolicy.ReadOnly, [], Token, useWorktree: true));
        Assert.Empty(probe.Created);
        Assert.Single(repository.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPersistence_CleansOnlyUnownedWorktreeWithIndependentCancellation(bool cancelCaller)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, _) =>
            {
                if (cancelCaller) cancellation.Cancel();
                throw new IOException("synthetic persistence failure");
            }
        };
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        await Assert.ThrowsAsync<IOException>(() => factory.PrepareAsync(CreateProject(), null, "prompt",
            ApprovalPolicy.ReadOnly, [], cancellation.Token, useWorktree: true));
        Assert.Single(probe.Created);
        Assert.Single(probe.Removed);
        Assert.False(probe.RemovalWasCancelled);
        Assert.Null(repository.SavedTask);
        Assert.Empty(repository.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistenceMayHaveCommitted_ActiveAndArchivedOwnersPreventRemoval(bool archived)
    {
        var project = CreateProject();
        var repository = new TestWorkspaceRepository();
        repository.SaveTaskHandler = (task, _) =>
        {
            if (archived) repository.ArchivedTasks = [task];
            else repository.TasksByProject[project.Id] = [task];
            throw new IOException("synthetic post-commit failure");
        };
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        var exception = await Assert.ThrowsAsync<WorkspaceWorktreePreparationException>(() => factory.PrepareAsync(
            project, null, "prompt", ApprovalPolicy.ReadOnly, [], Token, useWorktree: true));
        Assert.Single(probe.Created);
        Assert.Empty(probe.Removed);
        Assert.False(string.IsNullOrWhiteSpace(exception.WorktreePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncertainOwnershipOrDirtyCleanup_PreservesRecoveryInformation(bool dirty)
    {
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (_, _) => throw new IOException("synthetic persistence failure")
        };
        if (!dirty) repository.GetTasksHandler = (_, _) => throw new IOException("synthetic lookup failure");
        var probe = new WorkspaceWorktreeProbe();
        if (dirty) probe.RemoveHandler = _ => throw new IOException("synthetic dirty worktree refusal");
        var factory = new WorkspaceTaskFactory(repository, probe);
        var exception = await Assert.ThrowsAsync<WorkspaceWorktreePreparationException>(() => factory.PrepareAsync(
            CreateProject(), null, "prompt", ApprovalPolicy.ReadOnly, [], Token, useWorktree: true));
        Assert.Equal(dirty ? 1 : 0, probe.Removed.Count);
        Assert.IsType<AggregateException>(exception.InnerException);
        Assert.EndsWith(Assert.Single(probe.Created).TaskId.ToString("N"), exception.WorktreePath);
    }

    [Fact]
    public async Task FailedCreation_DoesNotAttemptToRemoveUnknownPath()
    {
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe { CreateHandler = _ => throw new IOException("synthetic git failure") };
        var factory = new WorkspaceTaskFactory(repository, probe);
        await Assert.ThrowsAsync<IOException>(() => factory.PrepareAsync(CreateProject(), null, "prompt",
            ApprovalPolicy.ReadOnly, [], Token, useWorktree: true));
        Assert.Empty(probe.Removed);
        Assert.Null(repository.SavedTask);
    }

    [Fact]
    public async Task AttachmentFailure_AlsoCleansOnlyTheUnpersistedWorktree()
    {
        var repository = new TestWorkspaceRepository();
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.PrepareAsync(CreateProject(), null, "prompt",
            ApprovalPolicy.ReadOnly, [new("synthetic.txt", "synthetic.txt", "fixture", 7)], Token, useWorktree: true));
        Assert.Single(probe.Created);
        Assert.Single(probe.Removed);
        Assert.Null(repository.SavedTask);
    }

    [Fact]
    public async Task ExistingWorktree_ResumeKeepsPathAndOtherExplicitRootsWithoutCreatingAgain()
    {
        var project = CreateProject();
        var repository = new TestWorkspaceRepository { ProjectItems = [project] };
        var probe = new WorkspaceWorktreeProbe();
        var factory = new WorkspaceTaskFactory(repository, probe);
        var original = await factory.PrepareAsync(project, null, "first", ApprovalPolicy.ReadOnly, [], Token, useWorktree: true);
        var resumed = await factory.PrepareAsync(project, original, "follow-up", ApprovalPolicy.ReadOnly, [], Token);
        Assert.Equal(original.Id, resumed.Id);
        Assert.Equal(original.WorktreePath, resumed.WorktreePath);
        Assert.Equal(new[] { resumed.WorktreePath!, "extra-root" }, factory.GetAuthorizedRoots(project, resumed));
        Assert.Single(probe.Created);
        Assert.Empty(probe.Removed);
        Assert.Equal(2, repository.Messages.Count);
    }

    private static ProjectProfile CreateProject() => new(Guid.NewGuid(), "project", "project-root",
        ["project-root", "extra-root"], DateTimeOffset.UtcNow);
}
