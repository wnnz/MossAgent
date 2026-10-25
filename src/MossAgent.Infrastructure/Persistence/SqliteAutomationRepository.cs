using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteAutomationRepository(SqliteConnectionFactory connections) : IAutomationRepository
{
    public async Task<IReadOnlyList<AutomationSchedule>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.payload,s.revision,s.next_run_at,snap.payload
            FROM automation_schedules s LEFT JOIN automation_snapshots snap ON snap.schedule_id=s.id
            WHERE s.archived=0 ORDER BY s.rowid;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var schedules = new List<AutomationSchedule>();
        while (await reader.ReadAsync(cancellationToken))
            schedules.Add(JsonSerializer.Deserialize<AutomationSchedule>(reader.GetString(0))! with
            { Revision = reader.GetInt64(1), NextRunAt = ParseTime(reader.GetString(2)),
                AuthorizationSnapshot = ReadSnapshot(reader, 3) });
        return schedules;
    }

    public async Task<bool> SaveAsync(AutomationSchedule schedule, CancellationToken cancellationToken)
    {
        AutomationTiming.Validate(schedule);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = schedule.Revision == 0 ? """
            INSERT OR IGNORE INTO automation_schedules(id,payload,revision,enabled,next_run_at)
            VALUES($id,$payload,1,$enabled,$next);
            """ : """
            UPDATE automation_schedules SET payload=$payload,revision=revision+1,enabled=$enabled,next_run_at=$next
            WHERE id=$id AND revision=$revision AND archived=0;
            """;
        command.Parameters.AddWithValue("$id", schedule.Id.ToString());
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(schedule));
        command.Parameters.AddWithValue("$enabled", schedule.IsEnabled);
        command.Parameters.AddWithValue("$next", FormatTime(schedule.NextRunAt));
        command.Parameters.AddWithValue("$revision", schedule.Revision);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        await using var snapshot = connection.CreateCommand();
        snapshot.Transaction = transaction;
        snapshot.CommandText = schedule.AuthorizationSnapshot is null
            ? "DELETE FROM automation_snapshots WHERE schedule_id=$id;"
            : "INSERT OR REPLACE INTO automation_snapshots(schedule_id,payload) VALUES($id,$snapshot);";
        snapshot.Parameters.AddWithValue("$id", schedule.Id.ToString());
        if (schedule.AuthorizationSnapshot is not null)
            snapshot.Parameters.AddWithValue("$snapshot", SerializeSnapshot(schedule.AuthorizationSnapshot));
        await snapshot.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ArchiveAsync(Guid id, long revision, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE automation_schedules SET archived=1,enabled=0,revision=revision+1
            WHERE id=$id AND revision=$revision AND archived=0
            AND NOT EXISTS(SELECT 1 FROM automation_runs WHERE schedule_id=$id AND status=0);
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$revision", revision);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<AutomationRun?> TryClaimAsync(AutomationSchedule expected, DateTimeOffset now,
        DateTimeOffset nextRunAt, bool manual, CancellationToken cancellationToken)
    {
        AutomationTiming.Validate(expected);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var claim = connection.CreateCommand();
        claim.Transaction = transaction;
        claim.CommandText = """
            UPDATE automation_schedules SET revision=revision+1,next_run_at=$next
            WHERE id=$id AND revision=$revision AND archived=0 AND ($manual=1 OR (enabled=1 AND next_run_at<=$now))
            AND NOT EXISTS(SELECT 1 FROM automation_runs WHERE schedule_id=$id AND status=0);
            """;
        claim.Parameters.AddWithValue("$id", expected.Id.ToString());
        claim.Parameters.AddWithValue("$revision", expected.Revision);
        claim.Parameters.AddWithValue("$next", FormatTime(manual ? expected.NextRunAt : nextRunAt));
        claim.Parameters.AddWithValue("$now", FormatTime(now));
        claim.Parameters.AddWithValue("$manual", manual);
        if (await claim.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        var run = new AutomationRun(Guid.NewGuid(), expected.Id, Guid.NewGuid(), now,
            AutomationRunStatus.Running, manual);
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO automation_runs(id,schedule_id,task_id,started_at,status,manual)
            VALUES($id,$schedule,$task,$started,0,$manual);
            """;
        insert.Parameters.AddWithValue("$id", run.Id.ToString());
        insert.Parameters.AddWithValue("$schedule", run.ScheduleId.ToString());
        insert.Parameters.AddWithValue("$task", run.TaskId.ToString());
        insert.Parameters.AddWithValue("$started", FormatTime(now));
        insert.Parameters.AddWithValue("$manual", manual);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return run;
    }

    public async Task<IReadOnlyList<AutomationRun>> GetRunsAsync(Guid scheduleId, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,task_id,started_at,status,manual,finished_at,error_code
            FROM automation_runs WHERE schedule_id=$id ORDER BY started_at DESC,rowid DESC LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$id", scheduleId.ToString());
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var runs = new List<AutomationRun>();
        while (await reader.ReadAsync(cancellationToken))
            runs.Add(new AutomationRun(Guid.Parse(reader.GetString(0)), scheduleId, Guid.Parse(reader.GetString(1)),
                ParseTime(reader.GetString(2)), (AutomationRunStatus)reader.GetInt32(3), reader.GetBoolean(4),
                reader.IsDBNull(5) ? null : ParseTime(reader.GetString(5)), reader.IsDBNull(6) ? null : reader.GetString(6)));
        return runs;
    }

    public async Task FinishAsync(AutomationRun run, CancellationToken cancellationToken)
    {
        if (run.Status == AutomationRunStatus.Running || run.FinishedAt is null)
            throw new ArgumentException("A final automation state is required.");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE automation_runs SET status=$status,finished_at=$finished,error_code=$error
            WHERE id=$id AND schedule_id=$schedule AND task_id=$task AND status=0;
            """;
        command.Parameters.AddWithValue("$id", run.Id.ToString());
        command.Parameters.AddWithValue("$schedule", run.ScheduleId.ToString());
        command.Parameters.AddWithValue("$task", run.TaskId.ToString());
        command.Parameters.AddWithValue("$status", (int)run.Status);
        command.Parameters.AddWithValue("$finished", FormatTime(run.FinishedAt.Value));
        command.Parameters.AddWithValue("$error", (object?)run.ErrorCode ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecoverAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE automation_runs SET status=$status,finished_at=$now,error_code='interrupted' WHERE status=0;";
        command.Parameters.AddWithValue("$status", (int)AutomationRunStatus.Interrupted);
        command.Parameters.AddWithValue("$now", FormatTime(now));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeSnapshot(AutomationAuthorizationSnapshot snapshot)
    {
        var payload = JsonSerializer.Serialize(snapshot);
        return payload.Length is > 262144 or < 2
            ? throw new ArgumentException("The automation authorization snapshot is out of bounds.")
            : payload;
    }

    private static AutomationAuthorizationSnapshot? ReadSnapshot(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        try { return JsonSerializer.Deserialize<AutomationAuthorizationSnapshot>(reader.GetString(ordinal)); }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException) { return null; }
    }

    private static string FormatTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
