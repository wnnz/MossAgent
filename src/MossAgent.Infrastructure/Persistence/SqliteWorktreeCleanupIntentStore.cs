using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorktreeCleanupIntentStore(
    SqliteConnectionFactory connections,
    TimeProvider timeProvider) : IWorktreeCleanupIntentService
{
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");

    public async Task BeginAsync(Guid projectId, Guid taskId, string targetPath,
        string quarantinePath, CancellationToken cancellationToken)
    {
        Validate(projectId, taskId, targetPath, quarantinePath);
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO worktree_cleanup_intents(
                task_id,project_id,target_path,quarantine_path,stage,state,
                owner_instance_id,error_code,created_at,updated_at)
            VALUES($task,$project,$target,$quarantine,0,0,$owner,NULL,$now,$now)
            ON CONFLICT(task_id) DO UPDATE SET
                project_id=excluded.project_id,
                target_path=excluded.target_path,
                quarantine_path=excluded.quarantine_path,
                stage=0,
                state=0,
                owner_instance_id=excluded.owner_instance_id,
                error_code=NULL,
                updated_at=excluded.updated_at
            WHERE worktree_cleanup_intents.state IN (2,3);
            """;
        AddIdentity(command, projectId, taskId, targetPath, quarantinePath, now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException(
                "An unfinished worktree cleanup already owns this task.");
    }

    public Task MarkQuarantinedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken) =>
        AdvanceAsync(taskId, quarantinePath, WorktreeCleanupStage.Preparing,
            WorktreeCleanupStage.Quarantined, cancellationToken);

    public Task MarkRegistrationDeletedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken) =>
        AdvanceAsync(taskId, quarantinePath, WorktreeCleanupStage.Quarantined,
            WorktreeCleanupStage.RegistrationDeleted, cancellationToken);

    public Task MarkCompletedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken) =>
        ChangeStateAsync(taskId, quarantinePath, WorktreeCleanupStage.RegistrationDeleted,
            WorktreeCleanupState.Completed, null, cancellationToken);

    public Task MarkFailedAsync(Guid taskId, string quarantinePath, string errorCode,
        CancellationToken cancellationToken) =>
        ChangeStateAsync(taskId, quarantinePath, null,
            WorktreeCleanupState.Failed, SafeError(errorCode), cancellationToken);

    public async Task<IReadOnlyList<WorktreeCleanupIntent>> ReadIncompleteAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Invalid project identity.", nameof(projectId));
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM worktree_cleanup_intents
            WHERE project_id=$project AND (
                state IN (0,2) OR (
                    state=1 AND EXISTS(
                        SELECT 1 FROM agent_tasks task
                        WHERE task.id=worktree_cleanup_intents.task_id
                          AND task.worktree_path=worktree_cleanup_intents.target_path
                    )
                )
            )
            ORDER BY updated_at,task_id;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<WorktreeCleanupIntent>();
        while (await reader.ReadAsync(cancellationToken)) items.Add(Read(reader));
        return items;
    }

    public async Task AcknowledgeAsync(WorktreeCleanupIntent intent,
        CancellationToken cancellationToken)
    {
        Validate(intent.ProjectId, intent.TaskId, intent.TargetPath, intent.QuarantinePath);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_cleanup_intents
            SET state=3,updated_at=$now
            WHERE task_id=$task AND project_id=$project
              AND target_path=$target AND quarantine_path=$quarantine
              AND (
                  state=2
                  OR (state=0 AND owner_instance_id<>$owner)
                  OR (state=1 AND EXISTS(
                      SELECT 1 FROM agent_tasks task
                      WHERE task.id=worktree_cleanup_intents.task_id
                        AND task.worktree_path=worktree_cleanup_intents.target_path
                  ))
              );
            """;
        command.Parameters.AddWithValue("$task", intent.TaskId.ToString());
        command.Parameters.AddWithValue("$project", intent.ProjectId.ToString());
        command.Parameters.AddWithValue("$target", intent.TargetPath);
        command.Parameters.AddWithValue("$quarantine", intent.QuarantinePath);
        command.Parameters.AddWithValue("$owner", InstanceId);
        command.Parameters.AddWithValue("$now", Now.ToString("O"));
        await RequireOneAsync(command, cancellationToken);
    }

    private async Task AdvanceAsync(Guid taskId, string quarantinePath,
        WorktreeCleanupStage expected, WorktreeCleanupStage next,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_cleanup_intents
            SET stage=$next,updated_at=$now,error_code=NULL
            WHERE task_id=$task AND quarantine_path=$quarantine
              AND owner_instance_id=$owner AND state=0 AND stage=$expected;
            """;
        AddTransition(command, taskId, quarantinePath);
        command.Parameters.AddWithValue("$expected", (int)expected);
        command.Parameters.AddWithValue("$next", (int)next);
        await RequireOneAsync(command, cancellationToken);
    }

    private async Task ChangeStateAsync(Guid taskId, string quarantinePath,
        WorktreeCleanupStage? expected, WorktreeCleanupState state,
        string? errorCode, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE worktree_cleanup_intents
            SET state=$state,updated_at=$now,error_code=$error
            WHERE task_id=$task AND quarantine_path=$quarantine
              AND owner_instance_id=$owner AND state=0
              {(expected is null ? string.Empty : "AND stage=$stage")};
            """;
        AddTransition(command, taskId, quarantinePath);
        command.Parameters.AddWithValue("$state", (int)state);
        command.Parameters.AddWithValue("$error", (object?)errorCode ?? DBNull.Value);
        if (expected is not null)
            command.Parameters.AddWithValue("$stage", (int)expected.Value);
        await RequireOneAsync(command, cancellationToken);
    }

    private void AddTransition(SqliteCommand command, Guid taskId, string quarantinePath)
    {
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$quarantine", quarantinePath);
        command.Parameters.AddWithValue("$owner", InstanceId);
        command.Parameters.AddWithValue("$now", Now.ToString("O"));
    }

    private void AddIdentity(SqliteCommand command, Guid projectId, Guid taskId,
        string targetPath, string quarantinePath, DateTimeOffset now)
    {
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$target", targetPath);
        command.Parameters.AddWithValue("$quarantine", quarantinePath);
        command.Parameters.AddWithValue("$owner", InstanceId);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
    }

    private DateTimeOffset Now => timeProvider.GetUtcNow();

    private static void Validate(Guid projectId, Guid taskId, string targetPath,
        string quarantinePath)
    {
        if (projectId == Guid.Empty || taskId == Guid.Empty
            || !Path.IsPathFullyQualified(targetPath)
            || !Path.IsPathFullyQualified(quarantinePath)
            || targetPath.Length > 32767 || quarantinePath.Length > 32767
            || targetPath.Contains('\0') || quarantinePath.Contains('\0')
            || string.Equals(targetPath, quarantinePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid worktree cleanup intent.");
    }

    private static string SafeError(string errorCode) =>
        string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 64
            ? "worktree_cleanup_failed" : errorCode;

    private static async Task RequireOneAsync(SqliteCommand command,
        CancellationToken cancellationToken)
    {
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Worktree cleanup intent changed concurrently.");
    }

    private const string Columns =
        "task_id,project_id,target_path,quarantine_path,stage,state,owner_instance_id,error_code,created_at,updated_at";

    private static WorktreeCleanupIntent Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
        reader.GetString(2), reader.GetString(3), (WorktreeCleanupStage)reader.GetInt32(4),
        (WorktreeCleanupState)reader.GetInt32(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        DateTimeOffset.Parse(reader.GetString(8)), DateTimeOffset.Parse(reader.GetString(9)));
}
