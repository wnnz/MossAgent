using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteBrowserSessionJournal(SqliteConnectionFactory connections) : IBrowserSessionJournal
{
    private const int TaskReadLimit = 256;

    public async Task<bool> RecordSessionAsync(Guid taskId, BrowserEngine engine,
        BrowserProfileMode profileMode, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // A missing or archived task yields zero rows instead of a foreign key error,
        // so callers can distinguish "cannot journal" from a storage failure.
        command.CommandText = """
            INSERT INTO browser_session_records(id,task_id,engine,profile_mode,state,created_at,updated_at)
            SELECT $id,$task,$engine,$profile,0,$now,$now
            WHERE EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task
                AND NOT EXISTS(SELECT 1 FROM archived_agent_tasks WHERE task_id=$task));
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        command.Parameters.AddWithValue("$engine", (int)engine);
        command.Parameters.AddWithValue("$profile", (int)profileMode);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public Task RecordDetachedAsync(Guid taskId, CancellationToken cancellationToken) =>
        MarkSessionAsync(taskId, BrowserSessionState.Detached, cancellationToken);

    public Task RecordDisconnectedAsync(Guid taskId, CancellationToken cancellationToken) =>
        MarkSessionAsync(taskId, BrowserSessionState.Disconnected, cancellationToken);

    public async Task<Guid?> RecordActionAsync(Guid taskId, BrowserActionKind kind, string? targetHash,
        CancellationToken cancellationToken)
    {
        var intentId = Guid.NewGuid();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // The intent pins itself to the task's still-attached session; a missing row
        // yields no insert so the caller can refuse an unaudited side effect.
        command.CommandText = """
            INSERT INTO browser_action_intents(id,session_id,kind,target_hash,state,created_at,updated_at)
            SELECT $id,s.id,$kind,$target,0,$now,$now
            FROM browser_session_records s
            WHERE s.task_id=$task AND s.state=0
            ORDER BY s.created_at DESC,s.id DESC
            LIMIT 1
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$id", intentId.ToString("D"));
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        command.Parameters.AddWithValue("$kind", ToStorage(kind));
        command.Parameters.AddWithValue("$target", (object?)targetHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        var stored = await command.ExecuteScalarAsync(cancellationToken);
        return stored is null ? null : Guid.ParseExact((string)stored, "D");
    }

    public Task ResolveActionAsync(Guid intentId, BrowserActionIntentState state,
        CancellationToken cancellationToken)
    {
        if (state == BrowserActionIntentState.Committed)
            throw new ArgumentException("An intent cannot be resolved back into the committed state.", nameof(state));
        return WorktreeIntentSqlite.UpdateAsync(connections, """
            UPDATE browser_action_intents SET state=$state,updated_at=$now
            WHERE id=$id AND state=0;
            """, cancellationToken, ("$id", intentId.ToString("D")), ("$state", (int)state));
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var sessions = await WorktreeIntentSqlite.UpdateAsync(connections, """
            UPDATE browser_session_records SET state=3,updated_at=$now WHERE state=0;
            """, cancellationToken).ConfigureAwait(false);
        var intents = await WorktreeIntentSqlite.UpdateAsync(connections, """
            UPDATE browser_action_intents SET state=4,updated_at=$now WHERE state=0;
            """, cancellationToken).ConfigureAwait(false);
        return sessions + intents;
    }

    public Task<IReadOnlyList<BrowserSessionRecord>> ReadSessionsAsync(Guid taskId,
        CancellationToken cancellationToken) =>
        WorktreeIntentSqlite.QueryAsync(connections, $"""
            SELECT id,task_id,engine,profile_mode,state,created_at,updated_at
            FROM browser_session_records WHERE task_id=$task ORDER BY created_at,id LIMIT {TaskReadLimit};
            """, ReadSession, cancellationToken, ("$task", taskId.ToString("D")));

    public Task<IReadOnlyList<BrowserActionIntentRecord>> ReadIntentsAsync(Guid taskId,
        CancellationToken cancellationToken) =>
        WorktreeIntentSqlite.QueryAsync(connections, $"""
            SELECT i.id,i.session_id,i.kind,i.target_hash,i.state,i.created_at,i.updated_at
            FROM browser_action_intents i
            JOIN browser_session_records s ON s.id=i.session_id
            WHERE s.task_id=$task ORDER BY i.created_at,i.id LIMIT {TaskReadLimit};
            """, ReadIntent, cancellationToken, ("$task", taskId.ToString("D")));

    private Task MarkSessionAsync(Guid taskId, BrowserSessionState state, CancellationToken token) =>
        WorktreeIntentSqlite.UpdateAsync(connections, """
            UPDATE browser_session_records SET state=$state,updated_at=$now
            WHERE task_id=$task AND state=0;
            """, token, ("$task", taskId.ToString("D")), ("$state", (int)state));

    private static BrowserSessionRecord ReadSession(SqliteDataReader reader) => new(
        Guid.ParseExact(reader.GetString(0), "D"),
        Guid.ParseExact(reader.GetString(1), "D"),
        (BrowserEngine)reader.GetInt32(2),
        (BrowserProfileMode)reader.GetInt32(3),
        (BrowserSessionState)reader.GetInt32(4),
        DateTimeOffset.Parse(reader.GetString(5)),
        DateTimeOffset.Parse(reader.GetString(6)));

    private static BrowserActionIntentRecord ReadIntent(SqliteDataReader reader) => new(
        Guid.ParseExact(reader.GetString(0), "D"),
        Guid.ParseExact(reader.GetString(1), "D"),
        FromStorage(reader.GetString(2)),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        (BrowserActionIntentState)reader.GetInt32(4),
        DateTimeOffset.Parse(reader.GetString(5)),
        DateTimeOffset.Parse(reader.GetString(6)));

    private static string ToStorage(BrowserActionKind kind) => kind switch
    {
        BrowserActionKind.Navigate => "navigate",
        BrowserActionKind.NavigateBack => "navigate-back",
        BrowserActionKind.NavigateForward => "navigate-forward",
        BrowserActionKind.Reload => "reload",
        BrowserActionKind.NewTab => "new-tab",
        BrowserActionKind.CloseTab => "close-tab",
        BrowserActionKind.Click => "click",
        BrowserActionKind.Type => "type",
        BrowserActionKind.SelectOption => "select-option",
        BrowserActionKind.Download => "download",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown browser action kind.")
    };

    private static BrowserActionKind FromStorage(string kind) => kind switch
    {
        "navigate" => BrowserActionKind.Navigate,
        "navigate-back" => BrowserActionKind.NavigateBack,
        "navigate-forward" => BrowserActionKind.NavigateForward,
        "reload" => BrowserActionKind.Reload,
        "new-tab" => BrowserActionKind.NewTab,
        "close-tab" => BrowserActionKind.CloseTab,
        "click" => BrowserActionKind.Click,
        "type" => BrowserActionKind.Type,
        "select-option" => BrowserActionKind.SelectOption,
        "download" => BrowserActionKind.Download,
        _ => throw new InvalidOperationException("Invalid stored browser action kind.")
    };
}
