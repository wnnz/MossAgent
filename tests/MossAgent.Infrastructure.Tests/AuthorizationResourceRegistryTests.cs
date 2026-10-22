using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AuthorizationResourceRegistryTests
{
    [Fact]
    public async Task RegistryTracksHeartbeatReleaseAndCrossInstanceOutstandingRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var first = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var second = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var leaseId = Guid.NewGuid();
        await first.RegisterAsync(leaseId, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        Assert.Equal(1, await second.CountOutstandingAsync(
            fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token));

        await first.HeartbeatAsync(leaseId, fixture.Token);
        await first.ReleaseAsync(leaseId, fixture.Token);

        Assert.Equal(0, await second.CountOutstandingAsync(
            fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token));
    }

    [Fact]
    public async Task StaleAbandonmentRequiresAnExpiredHeartbeatAndLeavesAnAuditResolution()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var registry = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var leaseId = Guid.NewGuid();
        await registry.RegisterAsync(leaseId, fixture.Project.Id, fixture.Project.AuthorizationRevision, fixture.Token);
        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-30);
        Assert.Equal(0, await registry.AbandonStaleAsync(
            fixture.Project.Id, [leaseId], staleBefore, fixture.Token));

        await using (var connection = fixture.Connections.Create())
        {
            await connection.OpenAsync(fixture.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE authorization_resource_leases SET heartbeat_at=$heartbeat WHERE lease_id=$lease;";
            command.Parameters.AddWithValue("$heartbeat", staleBefore.AddMinutes(-1).ToString("O"));
            command.Parameters.AddWithValue("$lease", leaseId.ToString());
            await command.ExecuteNonQueryAsync(fixture.Token);
        }

        var outstanding = Assert.Single(await registry.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(leaseId, outstanding.LeaseId);
        Assert.Equal(1, await registry.AbandonStaleAsync(
            fixture.Project.Id, [leaseId], staleBefore, fixture.Token));
        Assert.Empty(await registry.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM authorization_resource_leases WHERE resolution='abandoned';"));
    }
}
