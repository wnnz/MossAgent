using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteWorktreeCreationIntentStore(SqliteConnectionFactory connections,
    TimeProvider timeProvider)
{
    public async Task BeginAsync(Guid projectId, Guid taskId, string repositoryRoot, string? baseReference,
        string targetPath, CancellationToken cancellationToken)
    {
        Validate(projectId, taskId, repositoryRoot, baseReference, targetPath);
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO worktree_creation_intents(
                task_id,project_id,repository_root,base_reference,target_path,status,created_at,updated_at)
            VALUES($task,$project,$repository,$base,$target,0,$now,$now);
            """;
        AddIdentity(command, projectId, taskId, repositoryRoot, baseReference, targetPath);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkClaimedAsync(Guid taskId, string targetPath, DirectoryObjectIdentity identity,
        CancellationToken cancellationToken)
    {
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents
            SET status=1,volume_id=$volume,object_id=$object,updated_at=$now,error_code=NULL
            WHERE task_id=$task AND target_path=$target AND status=0;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$target", targetPath);
        command.Parameters.AddWithValue("$volume", identity.VolumeId);
        command.Parameters.AddWithValue("$object", identity.FileId);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        await RequireOneAsync(command, cancellationToken);
    }

    public async Task MarkGitCreatedAsync(Guid taskId, string targetPath, CancellationToken cancellationToken)
    {
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents
            SET status=2,updated_at=$now,error_code=NULL
            WHERE task_id=$task AND target_path=$target AND status IN (0,1);
            """;
        AddTaskTarget(command, taskId, targetPath, now);
        await RequireOneAsync(command, cancellationToken);
    }

    public async Task MarkFailedAsync(Guid taskId, string targetPath, string errorCode,
        CancellationToken cancellationToken)
    {
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents
            SET status=3,error_code=$error,updated_at=$now
            WHERE task_id=$task AND target_path=$target AND status IN (0,1,2);
            """;
        AddTaskTarget(command, taskId, targetPath, now);
        command.Parameters.AddWithValue("$error", errorCode.Length <= 64 ? errorCode : "worktree_creation_failed");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkConsumedAsync(Guid taskId, string targetPath, CancellationToken cancellationToken)
    {
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents
            SET status=4,updated_at=$now,error_code=NULL
            WHERE task_id=$task AND target_path=$target AND status IN (2,5);
            """;
        AddTaskTarget(command, taskId, targetPath, now);
        await RequireOneAsync(command, cancellationToken);
    }

    public async Task AcknowledgeAsync(Guid taskId, string targetPath, CancellationToken cancellationToken)
    {
        var now = Now;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents
            SET status=6,updated_at=$now
            WHERE task_id=$task AND target_path=$target AND status IN (3,5);
            """;
        AddTaskTarget(command, taskId, targetPath, now);
        await RequireOneAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Invalid project identity.", nameof(projectId));
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Columns}
            FROM worktree_creation_intents
            WHERE project_id=$project AND status IN (0,1,2,3,5)
            ORDER BY updated_at,task_id;
            """;
        command.Parameters.AddWithValue("$project", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<WorktreeCreationIntent>();
        while (await reader.ReadAsync(cancellationToken)) items.Add(Read(reader));
        return items;
    }

    public static async Task ReconcileStartupAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE worktree_creation_intents
            SET status=CASE WHEN EXISTS(
                    SELECT 1 FROM agent_tasks task
                    WHERE task.id=worktree_creation_intents.task_id
                      AND task.worktree_path=worktree_creation_intents.target_path
                ) THEN 4 ELSE 5 END,
                updated_at=strftime('%Y-%m-%dT%H:%M:%fZ','now')
            WHERE status IN (0,1,2);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DateTimeOffset Now => timeProvider.GetUtcNow();

    private static void Validate(Guid projectId, Guid taskId, string repositoryRoot, string? baseReference,
        string targetPath)
    {
        if (projectId == Guid.Empty || taskId == Guid.Empty || !Path.IsPathFullyQualified(repositoryRoot)
            || !Path.IsPathFullyQualified(targetPath)
            || repositoryRoot.Length > 32767 || targetPath.Length > 32767
            || repositoryRoot.Contains('\0') || targetPath.Contains('\0')
            || baseReference is { Length: > 4096 } || baseReference?.Contains('\0') == true)
            throw new ArgumentException("Invalid worktree creation intent.");
    }

    private static void AddIdentity(SqliteCommand command, Guid projectId, Guid taskId,
        string repositoryRoot, string? baseReference, string targetPath)
    {
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$project", projectId.ToString());
        command.Parameters.AddWithValue("$repository", repositoryRoot);
        command.Parameters.AddWithValue("$base", (object?)baseReference ?? DBNull.Value);
        command.Parameters.AddWithValue("$target", targetPath);
    }

    private static void AddTaskTarget(SqliteCommand command, Guid taskId, string targetPath, DateTimeOffset now)
    {
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$target", targetPath);
        command.Parameters.AddWithValue("$now", now.ToString("O"));
    }

    private static async Task RequireOneAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Worktree creation intent changed concurrently.");
    }

    private const string Columns =
        "task_id,project_id,repository_root,base_reference,target_path,status,volume_id,object_id,error_code,created_at,updated_at";

    private static WorktreeCreationIntent Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
        (WorktreeCreationStatus)reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
        DateTimeOffset.Parse(reader.GetString(9)), DateTimeOffset.Parse(reader.GetString(10)));
}
