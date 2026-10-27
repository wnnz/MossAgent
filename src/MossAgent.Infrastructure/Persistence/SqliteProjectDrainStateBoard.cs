using System.Globalization;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Authorization;

namespace MossAgent.Infrastructure.Persistence;

/// <summary>SQLite-backed drain-state board shared by every process on the same workspace
/// database. Publishes upsert this instance's (project, instance) row — Idle deletes it —
/// and aggregation folds all rows for a project to the worst state, upgrading rows untouched
/// past the staleness window to CleanupUnknown.</summary>
public sealed class SqliteProjectDrainStateBoard(SqliteConnectionFactory connections)
    : IProjectDrainStateBoard
{
    public async Task PublishAsync(Guid projectId, Guid instanceId, AuthorizationDrainState state,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = state == AuthorizationDrainState.Idle
            ? "DELETE FROM project_drain_states WHERE project_id=$project AND instance_id=$instance;"
            : """
              INSERT INTO project_drain_states(project_id,instance_id,state,updated_at)
              VALUES($project,$instance,$state,$at)
              ON CONFLICT(project_id,instance_id) DO UPDATE SET state=$state,updated_at=$at;
              """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$instance", instanceId.ToString());
        command.Parameters.AddWithValue("$state", (long)state);
        command.Parameters.AddWithValue("$at", at.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM project_drain_states WHERE instance_id=$instance;";
        command.Parameters.AddWithValue("$instance", instanceId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AuthorizationDrainState> AggregateAsync(Guid projectId,
        DateTimeOffset staleBefore, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT instance_id,state,updated_at FROM project_drain_states "
            + "WHERE project_id=$project;";
        command.Parameters.AddWithValue("$project", projectId.ToString());
        var worst = AuthorizationDrainState.Idle;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new ProjectDrainStateRow(projectId, Guid.Parse(reader.GetString(0)),
                (AuthorizationDrainState)reader.GetInt64(1),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture));
            var effective = row.UpdatedAt <= staleBefore
                ? AuthorizationDrainState.CleanupUnknown
                : row.State;
            if (effective > worst) worst = effective;
        }
        return worst;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
