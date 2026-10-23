using MossAgent.Infrastructure.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AuthorizationResourceAdmissionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task NonCurrentRevisionCannotCreateAReservation(long revision)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var registry = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => registry.RegisterAsync(
            Guid.NewGuid(), fixture.Project.Id, revision, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM authorization_resource_leases;"));
    }

    [Fact]
    public async Task MissingProjectCannotCreateAReservation()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var registry = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => registry.RegisterAsync(
            Guid.NewGuid(), Guid.NewGuid(), 1, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM authorization_resource_leases;"));
    }

    [Fact]
    public async Task RevisionUpdateRejectsNewOldReservationsButKeepsPreviouslyAdmittedResourcesVisible()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var registry = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var peer = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var lease = Guid.NewGuid();
        await registry.RegisterAsync(lease, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        await fixture.Repository.SaveProjectAsync(fixture.Project with { AuthorizedDirectories = [] }, fixture.Token);
        var current = (await fixture.Repository.ReadAuthorizationAsync(fixture.Project.Id, fixture.Token))!;
        Assert.True(current.AuthorizationRevision > fixture.Project.AuthorizationRevision);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => registry.RegisterAsync(
            Guid.NewGuid(), fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token));
        Assert.Equal(lease, Assert.Single(await peer.ListOutstandingAsync(fixture.Project.Id, fixture.Token)).LeaseId);
        await registry.ReleaseAsync(lease, fixture.Token);
        var fresh = Guid.NewGuid();
        await registry.RegisterAsync(fresh, current.Id, current.AuthorizationRevision, fixture.Token);
        Assert.Equal(0, await peer.CountOutstandingAsync(current.Id, fixture.Project.AuthorizationRevision, fixture.Token));
        Assert.Equal(fresh, Assert.Single(await peer.ListOutstandingAsync(current.Id, fixture.Token)).LeaseId);
        await registry.ReleaseAsync(fresh, fixture.Token);
        Assert.Empty(await peer.ListOutstandingAsync(current.Id, fixture.Token));
    }
}
