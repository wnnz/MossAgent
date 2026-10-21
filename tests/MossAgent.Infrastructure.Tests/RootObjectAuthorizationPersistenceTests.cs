using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class RootObjectAuthorizationPersistenceTests
{
    [Fact]
    public async Task LegacyRootsRequireConfirmationAndRenamingCannotReauthorizeReplacedObjects()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var identities = new RootIdentityProbe();
        var changes = new ProjectAuthorizationChanges();
        var repository = new SqliteWorkspaceRepository(fixture.Connections, authorizationChanges: changes, rootIdentities: identities);
        var original = (await repository.ReadAuthorizationAsync(fixture.Project.Id, fixture.Token))!;
        Assert.Empty(original.RootAuthorizations);
        await using var monitor = new ProjectAuthorizationMonitor(repository, changes, roots: identities);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(original, fixture.Token));
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => repository.SaveAuthorizationAsync(original with { Name = "rename" }, fixture.Token));
        var confirmed = await repository.ConfirmRootObjectsAsync(original, fixture.Token);
        Assert.Equal(2, confirmed.AuthorizationRevision);
        var grant = Assert.Single(confirmed.RootAuthorizations);
        var renamed = await repository.SaveAuthorizationAsync(confirmed with { Name = "safe rename" }, fixture.Token);
        Assert.Equal(2, renamed.AuthorizationRevision);
        Assert.Equal(grant, Assert.Single(renamed.RootAuthorizations));
        await using (var lease = await monitor.AcquireAsync(renamed, fixture.Token))
        {
            Assert.Equal(1, identities.ActiveSessions);
            identities.Replace(renamed.PrimaryDirectory);
            await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.ValidateAsync(renamed, fixture.Token));
            Assert.True(lease.IsRevoked);
        }
        Assert.Equal(0, identities.ActiveSessions);
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => repository.SaveAuthorizationAsync(renamed with { Name = "cannot replace grant" }, fixture.Token));
        Assert.Equal(renamed.RootAuthorizations,
            (await repository.ReadAuthorizationAsync(renamed.Id, fixture.Token))!.RootAuthorizations);
        var reauthorized = await repository.ConfirmRootObjectsAsync(renamed, fixture.Token);
        Assert.Equal(3, reauthorized.AuthorizationRevision);
        Assert.NotEqual(grant.GrantId, Assert.Single(reauthorized.RootAuthorizations).GrantId);
        await Assert.ThrowsAsync<ProjectAuthorizationConflictException>(() => repository.ConfirmRootObjectsAsync(renamed, fixture.Token));
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(renamed, fixture.Token));
        await using var next = await monitor.AcquireAsync(reauthorized, fixture.Token);
        Assert.False(next.IsRevoked);
    }

    [Fact]
    public async Task NewRootsAreEnrolledAtomicallyAndExistingGrantsSurviveScopeChanges()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var identities = new RootIdentityProbe();
        var repository = new SqliteWorkspaceRepository(fixture.Connections, rootIdentities: identities);
        var project = fixture.Project with { Id = Guid.NewGuid() };
        var saved = await repository.SaveAuthorizationAsync(project, fixture.Token);
        Assert.Equal(1, saved.AuthorizationRevision);
        var initial = Assert.Single(saved.RootAuthorizations);
        var expanded = await repository.SaveAuthorizationAsync(saved with { AuthorizedDirectories = [saved.PrimaryDirectory, "additional"] }, fixture.Token);
        Assert.Equal(2, expanded.AuthorizationRevision);
        Assert.Equal(initial, expanded.RootAuthorizations[0]);
        Assert.Equal(2, expanded.RootAuthorizations.Count);
        var reopened = new SqliteWorkspaceRepository(fixture.Connections);
        Assert.Equal(expanded.RootAuthorizations, (await reopened.ReadAuthorizationAsync(saved.Id, fixture.Token))!.RootAuthorizations);
        await fixture.ExecuteAsync("CREATE TRIGGER reject_root_write BEFORE UPDATE ON project_root_authorizations BEGIN SELECT RAISE(ABORT,'synthetic root write failure'); END;");
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => repository.ConfirmRootObjectsAsync(expanded, fixture.Token));
        var unchanged = (await reopened.ReadAuthorizationAsync(saved.Id, fixture.Token))!;
        Assert.Equal(expanded.AuthorizationRevision, unchanged.AuthorizationRevision);
        Assert.Equal(expanded.RootAuthorizations, unchanged.RootAuthorizations);
        Assert.Equal(0, identities.ActiveSessions);
    }

    [Fact]
    public async Task RemovingPrimaryOrAllRootsNeverRequiresReadingOrRegrantingRemovedObjects()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var identities = new RootIdentityProbe();
        var changes = new ProjectAuthorizationChanges();
        var repository = new SqliteWorkspaceRepository(fixture.Connections, authorizationChanges: changes, rootIdentities: identities);
        var initial = await repository.ConfirmRootObjectsAsync(fixture.Project, fixture.Token);
        var expanded = await repository.SaveAuthorizationAsync(initial with { AuthorizedDirectories = [initial.PrimaryDirectory, "additional"] }, fixture.Token);
        await using var monitor = new ProjectAuthorizationMonitor(repository, changes, roots: identities);
        await using var lease = await monitor.AcquireAsync(expanded, fixture.Token);
        var reduced = await repository.SaveAuthorizationAsync(expanded with { AuthorizedDirectories = ["additional"] }, fixture.Token);
        Assert.True(lease.IsRevoked);
        Assert.Equal(expanded.RootAuthorizations[1], Assert.Single(reduced.RootAuthorizations));
        Assert.DoesNotContain(reduced.RootAuthorizations, grant => grant.Identity.Path == initial.PrimaryDirectory);
        identities.Override = (_, _) => throw new InvalidOperationException("Revoking all roots must not inspect any directory.");
        var revoked = await repository.SaveAuthorizationAsync(reduced with { AuthorizedDirectories = [] }, fixture.Token);
        Assert.Equal(reduced.AuthorizationRevision + 1, revoked.AuthorizationRevision);
        Assert.Empty(revoked.RootAuthorizations);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(revoked, fixture.Token));
        await Assert.ThrowsAsync<ProjectRootAuthorizationException>(() => repository.ConfirmRootObjectsAsync(revoked, fixture.Token));
        Assert.Empty((await repository.ReadAuthorizationAsync(initial.Id, fixture.Token))!.RootAuthorizations);
    }
}
