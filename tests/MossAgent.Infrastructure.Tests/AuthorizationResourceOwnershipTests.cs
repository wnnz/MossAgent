using MossAgent.Application.Authorization;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AuthorizationResourceOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherInstanceCannotRenewOrReleaseAnOwnedLease(bool release)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var owner = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var other = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var leaseId = Guid.NewGuid();
        await owner.RegisterAsync(leaseId, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => release
            ? other.ReleaseAsync(leaseId, fixture.Token) : other.HeartbeatAsync(leaseId, fixture.Token));
        Assert.Single(await other.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        await owner.HeartbeatAsync(leaseId, fixture.Token);
        await owner.ReleaseAsync(leaseId, fixture.Token);
        await owner.ReleaseAsync(leaseId, fixture.Token);
        Assert.Empty(await other.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedConfirmationSelectionCannotPartiallyAbandonResources(bool secondIsFresh)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var registry = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await registry.RegisterAsync(first, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        if (secondIsFresh)
            await registry.RegisterAsync(second, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        await MakeStaleAsync(fixture, first);
        await using var monitor = new ProjectAuthorizationMonitor(fixture.Repository,
            new ProjectAuthorizationChanges(), resources: registry);
        await Assert.ThrowsAsync<AuthorizationResourceChangedException>(() => monitor.AbandonStaleResourcesAsync(
            fixture.Project.Id, [first, second], TimeSpan.FromSeconds(30), fixture.Token));
        var outstanding = await registry.ListOutstandingAsync(fixture.Project.Id, fixture.Token);
        Assert.Contains(outstanding, item => item.LeaseId == first);
        Assert.Equal(secondIsFresh ? 2 : 1, outstanding.Count);
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM authorization_resource_leases WHERE resolution='abandoned';"));
    }

    [Fact]
    public async Task ExplicitStaleConfirmationCanResolveAllSelectedForeignLeasesAtomically()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var owner = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var other = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await owner.RegisterAsync(first, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        await owner.RegisterAsync(second, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        await MakeStaleAsync(fixture, first);
        await MakeStaleAsync(fixture, second);
        await using var monitor = new ProjectAuthorizationMonitor(fixture.Repository,
            new ProjectAuthorizationChanges(), resources: other);
        Assert.Equal(2, await monitor.AbandonStaleResourcesAsync(fixture.Project.Id,
            [first, second, first], TimeSpan.FromSeconds(30), fixture.Token));
        Assert.Empty(await owner.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.HeartbeatAsync(first, fixture.Token));
        await owner.ReleaseAsync(first, fixture.Token);
        Assert.Equal(2, await fixture.CountAsync(
            "SELECT COUNT(*) FROM authorization_resource_leases WHERE resolution='abandoned';"));
    }

    private static async Task MakeStaleAsync(SqliteFailurePersistenceFixture fixture, Guid leaseId)
    {
        await using var connection = fixture.Connections.Create();
        await connection.OpenAsync(fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE authorization_resource_leases SET heartbeat_at=$time WHERE lease_id=$lease;";
        command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"));
        command.Parameters.AddWithValue("$lease", leaseId.ToString());
        await command.ExecuteNonQueryAsync(fixture.Token);
    }
}
