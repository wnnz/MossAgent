using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteGitCommitDraftTests
{
    [Fact]
    public async Task DraftsRoundTripAcrossRepositoryInstances()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteGitCommitDraftRepository(fixture.Connections);
        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "  提交说明\r\n继续", 0), fixture.Token));
        var reopened = new SqliteGitCommitDraftRepository(fixture.Connections);
        Assert.Equal(new("  提交说明\r\n继续", 1), await reopened.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.True(await reopened.SaveAsync(new(fixture.Task.Id, "updated", 1), fixture.Token));
        Assert.Equal(new("updated", 2), await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM tool_executions;"));
    }

    [Fact]
    public async Task ConcurrentWritersAndStaleRevisionsCannotBypassTheExpectedRevision()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var first = new SqliteGitCommitDraftRepository(fixture.Connections);
        var second = new SqliteGitCommitDraftRepository(fixture.Connections);
        Assert.True(await first.SaveAsync(new(fixture.Task.Id, "first", 0), fixture.Token));
        var results = await Task.WhenAll(
            first.SaveAsync(new(fixture.Task.Id, "writer one", 1), fixture.Token),
            second.SaveAsync(new(fixture.Task.Id, "writer two", 1), fixture.Token));
        Assert.Single(results, result => result);
        Assert.Equal(2, (await first.GetAsync(fixture.Task.Id, fixture.Token))!.Revision);
        Assert.False(await second.SaveAsync(new(fixture.Task.Id, "stale insert", 0), fixture.Token));
        Assert.False(await second.SaveAsync(new(fixture.Task.Id, "stale update", 1), fixture.Token));
        Assert.True(await second.SaveAsync(new(fixture.Task.Id, "current", 2), fixture.Token));
        Assert.Equal(new("current", 3), await first.GetAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task EmptyTextDeletesTheRowAndDeleteHonorsTheExpectedRevision()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteGitCommitDraftRepository(fixture.Connections);
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "keep", 0), fixture.Token));
        Assert.False(await repository.SaveAsync(new(fixture.Task.Id, string.Empty, 2), fixture.Token));
        Assert.Equal(new("keep", 1), await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, string.Empty, 1), fixture.Token));
        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
        // A revision-0 empty save is an unconditional ensure-absent and clears unknown rows.
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "again", 0), fixture.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, string.Empty, 0), fixture.Token));
        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.False(await repository.DeleteAsync(fixture.Task.Id, 4, fixture.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "final", 0), fixture.Token));
        Assert.False(await repository.DeleteAsync(fixture.Task.Id, 2, fixture.Token));
        Assert.True(await repository.DeleteAsync(fixture.Task.Id, 1, fixture.Token));
        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.True(await repository.DeleteAsync(fixture.Task.Id, 0, fixture.Token));
    }

    [Fact]
    public async Task TaskDeletionCascadesAndMissingTasksCannotBeWritten()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteGitCommitDraftRepository(fixture.Connections);
        Assert.False(await repository.SaveAsync(new(Guid.NewGuid(), "orphan", 0), fixture.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "kept", 0), fixture.Token));
        await fixture.ExecuteAsync($"DELETE FROM agent_tasks WHERE id='{fixture.Task.Id}';");
        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM git_commit_drafts;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task InvalidUpdatesAndCancellationAreRejectedWithoutTouchingStorage()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteGitCommitDraftRepository(fixture.Connections);
        GitCommitDraftUpdate[] invalid =
        [
            new(fixture.Task.Id, new string('x', GitCommitDraft.MaximumTextLength + 1), 0),
            new(fixture.Task.Id, "invalid\0value", 0),
            new(fixture.Task.Id, "negative", -1),
            new(Guid.Empty, "no task", 0),
        ];
        foreach (var update in invalid)
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(update, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(Guid.Empty, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.DeleteAsync(Guid.Empty, 0, fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.DeleteAsync(fixture.Task.Id, -1, fixture.Token));
        Assert.True(await repository.SaveAsync(new(fixture.Task.Id, "line\r\nnext\tend", 0), fixture.Token));
        Assert.Equal(new("line\r\nnext\tend", 1), await repository.GetAsync(fixture.Task.Id, fixture.Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.SaveAsync(new(fixture.Task.Id, "cancelled", 1), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetAsync(fixture.Task.Id, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.DeleteAsync(fixture.Task.Id, 1, cancellation.Token));
        Assert.Equal(new("line\r\nnext\tend", 1), await repository.GetAsync(fixture.Task.Id, fixture.Token));
    }
}
