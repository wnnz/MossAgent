using Microsoft.Data.Sqlite;
using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectAuthorizationPersistenceTests
{
    [Fact]
    public async Task ChangesIncrementVersionAndStaleSaveCannotOverwriteOrPublish()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var changes = new ProjectAuthorizationChanges();
        var notifications = new List<long>();
        changes.Changed += (_, revision) => notifications.Add(revision);
        var repository = new SqliteWorkspaceRepository(fixture.Connections, authorizationChanges: changes);
        var original = fixture.Project;
        await repository.SaveProjectAsync(original with { Name = "renamed" }, fixture.Token);
        var renamed = (await repository.ReadAuthorizationAsync(original.Id, fixture.Token))!;
        Assert.Equal(1, renamed.AuthorizationRevision);
        await repository.SaveProjectAsync(renamed with { AuthorizedDirectories = [renamed.PrimaryDirectory, "additional"] }, fixture.Token);
        var changed = (await repository.ReadAuthorizationAsync(original.Id, fixture.Token))!;
        Assert.Equal(2, changed.AuthorizationRevision);
        await Assert.ThrowsAsync<ProjectAuthorizationConflictException>(() => repository.SaveProjectAsync(original, fixture.Token));
        Assert.Equal(new long[] { 1, 2 }, notifications);
        await repository.SaveProjectAsync(changed with { AuthorizedDirectories = original.AuthorizedDirectories }, fixture.Token);
        var reopened = new SqliteWorkspaceRepository(fixture.Connections);
        var restored = (await reopened.ReadAuthorizationAsync(original.Id, fixture.Token))!;
        Assert.Equal(3, restored.AuthorizationRevision);
        Assert.Equal(original.AuthorizedDirectories, restored.AuthorizedDirectories);
        await using var monitor = new ProjectAuthorizationMonitor(reopened, changes);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(original, fixture.Token));
    }

    [Fact]
    public async Task ConcurrentRepositoriesAllowOnlyOneAuthorizationSave()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var other = new SqliteWorkspaceRepository(fixture.Connections);
        async Task<bool> SaveAsync(SqliteWorkspaceRepository repository, string root)
        {
            try { await repository.SaveProjectAsync(fixture.Project with { AuthorizedDirectories = [root] }, fixture.Token); return true; }
            catch (ProjectAuthorizationConflictException) { return false; }
        }
        var results = await Task.WhenAll(SaveAsync(fixture.Repository, "first"), SaveAsync(other, "second"));
        Assert.Single(results, success => success);
        Assert.Equal(2, (await other.ReadAuthorizationAsync(fixture.Project.Id, fixture.Token))!.AuthorizationRevision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task NewProjectRejectsInvalidInitialVersion(long revision)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var invalid = fixture.Project with { Id = Guid.NewGuid(), AuthorizationRevision = revision };
        await Assert.ThrowsAsync<ProjectAuthorizationConflictException>(() => fixture.Repository.SaveProjectAsync(invalid, fixture.Token));
        Assert.Null(await fixture.Repository.ReadAuthorizationAsync(invalid.Id, fixture.Token));
    }

    [Fact]
    public async Task CrossInstanceChangeRevokesExistingLeaseWithoutLocalNotification()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await using var monitor = new ProjectAuthorizationMonitor(fixture.Repository, new ProjectAuthorizationChanges());
        await using var lease = await monitor.AcquireAsync(fixture.Project, fixture.Token);
        await new SqliteWorkspaceRepository(fixture.Connections).SaveProjectAsync(fixture.Project with { AuthorizedDirectories = [] }, fixture.Token);
        try { await Task.Delay(Timeout.Infinite, lease.Revocation).WaitAsync(TimeSpan.FromSeconds(5), fixture.Token); }
        catch (OperationCanceledException) when (lease.IsRevoked) { }
        Assert.True(lease.IsRevoked);
    }
}
