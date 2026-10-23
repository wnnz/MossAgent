using MossAgent.Application.Authorization;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteAuthorizationResourceRegistry(
    SqliteConnectionFactory connections,
    TimeProvider timeProvider) : IAuthorizationResourceRegistry
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public async Task RegisterAsync(Guid leaseId, Guid projectId, long revision,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().ToString("O");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO authorization_resource_leases(
                lease_id,project_id,authorization_revision,instance_id,registered_at,heartbeat_at,released_at)
            SELECT $lease,id,authorization_revision,$instance,$now,$now,NULL
            FROM projects WHERE id=$project AND authorization_revision=$revision;
            """;
        command.Parameters.AddWithValue("$lease", leaseId.ToString());
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$revision", revision);
        command.Parameters.AddWithValue("$instance", _instanceId);
        command.Parameters.AddWithValue("$now", now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new WorkspaceAuthorizationException();
    }

    public async Task HeartbeatAsync(Guid leaseId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE authorization_resource_leases
            SET heartbeat_at=$now
            WHERE lease_id=$lease AND instance_id=$instance AND released_at IS NULL;
            """;
        command.Parameters.AddWithValue("$lease", leaseId.ToString());
        command.Parameters.AddWithValue("$instance", _instanceId);
        command.Parameters.AddWithValue("$now", timeProvider.GetUtcNow().ToString("O"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Authorization resource lease is no longer registered.");
    }

    public async Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE authorization_resource_leases
            SET heartbeat_at=$now,released_at=COALESCE(released_at,$now),
                resolution=COALESCE(resolution,'released'),resolved_by_instance_id=COALESCE(resolved_by_instance_id,$instance)
            WHERE lease_id=$lease AND instance_id=$instance;
            """;
        command.Parameters.AddWithValue("$lease", leaseId.ToString());
        command.Parameters.AddWithValue("$now", timeProvider.GetUtcNow().ToString("O"));
        command.Parameters.AddWithValue("$instance", _instanceId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Authorization resource lease is not owned by this instance.");
    }

    public async Task<int> CountOutstandingAsync(Guid projectId, long maximumRevision,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM authorization_resource_leases
            WHERE project_id=$project
              AND authorization_revision<=$revision
              AND released_at IS NULL;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$revision", maximumRevision);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<AuthorizationResourceLeaseSnapshot>> ListOutstandingAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lease_id,project_id,authorization_revision,instance_id,registered_at,heartbeat_at
            FROM authorization_resource_leases
            WHERE project_id=$project AND released_at IS NULL
            ORDER BY registered_at,lease_id;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<AuthorizationResourceLeaseSnapshot>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new(
                Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetInt64(2),
                reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4)),
                DateTimeOffset.Parse(reader.GetString(5))));
        }
        return items;
    }

    public async Task<int> AbandonStaleAsync(Guid projectId, IReadOnlyCollection<Guid> leaseIds,
        DateTimeOffset staleBefore, CancellationToken cancellationToken)
    {
        var ids = leaseIds.Distinct().ToArray();
        if (ids.Length is < 1 or > 128 || ids.Any(id => id == Guid.Empty))
            throw new ArgumentException("Invalid authorization resource confirmation.");
        var parameters = ids.Select((_, index) => "$lease" + index).ToArray();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            UPDATE authorization_resource_leases
            SET released_at=$now,resolution='abandoned',resolved_by_instance_id=$instance
            WHERE project_id=$project
              AND released_at IS NULL
              AND heartbeat_at<=$staleBefore
              AND lease_id IN ({string.Join(",", parameters)});
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$now", timeProvider.GetUtcNow().ToString("O"));
        command.Parameters.AddWithValue("$instance", _instanceId);
        command.Parameters.AddWithValue("$staleBefore", staleBefore.ToString("O"));
        for (var index = 0; index < ids.Length; index++)
            command.Parameters.AddWithValue(parameters[index], ids[index].ToString());
        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updated != ids.Length)
        {
            await transaction.RollbackAsync(cancellationToken);
            return 0;
        }
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }
}
