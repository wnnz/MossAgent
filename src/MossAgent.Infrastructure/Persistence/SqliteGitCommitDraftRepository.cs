using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteGitCommitDraftRepository(SqliteConnectionFactory connections,
    TimeProvider? timeProvider = null) : IGitCommitDraftRepository
{
    public Task<GitCommitDraft?> GetAsync(Guid taskId, CancellationToken cancellationToken) =>
        Task.Run(() => RunBoundedAsync(token => ReadCoreAsync(taskId, token), cancellationToken), cancellationToken);

    private async Task<GitCommitDraft?> ReadCoreAsync(Guid taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GitCommitDraftValidation.ValidateTaskId(taskId);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: true);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT text,revision FROM git_commit_drafts WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        string text;
        long revision;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            text = reader.GetString(0);
            revision = reader.GetInt64(1);
        }
        if (text.Length is < 1 or > GitCommitDraft.MaximumTextLength || revision <= 0)
            throw new InvalidOperationException("Invalid stored git commit draft.");
        await transaction.CommitAsync(cancellationToken);
        return new(text, revision);
    }

    public Task<bool> SaveAsync(GitCommitDraftUpdate update, CancellationToken cancellationToken) =>
        Task.Run(() => RunBoundedAsync(token => SaveCoreAsync(update, token), cancellationToken), cancellationToken);

    private async Task<bool> SaveCoreAsync(GitCommitDraftUpdate update, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GitCommitDraftValidation.Validate(update);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var saved = update.Text.Length == 0
            ? await DeleteCoreAsync(connection, transaction, update.TaskId, update.ExpectedRevision, cancellationToken)
            : await WriteCoreAsync(connection, transaction, update, cancellationToken);
        if (!saved) return false;
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> WriteCoreAsync(SqliteConnection connection, SqliteTransaction transaction,
        GitCommitDraftUpdate update, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO git_commit_drafts(task_id,text,revision,updated_at)
            SELECT $task,$text,1,datetime('now')
            WHERE EXISTS(SELECT 1 FROM agent_tasks WHERE id=$task)
            AND ($expected=0 OR EXISTS(SELECT 1 FROM git_commit_drafts WHERE task_id=$task AND revision=$expected))
            ON CONFLICT(task_id) DO UPDATE SET text=$text,revision=git_commit_drafts.revision+1,updated_at=datetime('now')
            WHERE git_commit_drafts.revision=$expected;
            """;
        command.Parameters.AddWithValue("$task", update.TaskId.ToString("D"));
        command.Parameters.AddWithValue("$text", update.Text);
        command.Parameters.AddWithValue("$expected", update.ExpectedRevision);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public Task<bool> DeleteAsync(Guid taskId, long expectedRevision, CancellationToken cancellationToken) =>
        Task.Run(() => RunBoundedAsync(token => DeleteGuardedAsync(taskId, expectedRevision, token), cancellationToken),
            cancellationToken);

    private async Task<bool> DeleteGuardedAsync(Guid taskId, long expectedRevision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GitCommitDraftValidation.ValidateDelete(taskId, expectedRevision);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var deleted = await DeleteCoreAsync(connection, transaction, taskId, expectedRevision, cancellationToken);
        if (!deleted) return false;
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> DeleteCoreAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, long expectedRevision, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = expectedRevision == 0
            ? "DELETE FROM git_commit_drafts WHERE task_id=$task;"
            : "DELETE FROM git_commit_drafts WHERE task_id=$task AND revision=$expected;";
        command.Parameters.AddWithValue("$task", taskId.ToString("D"));
        command.Parameters.AddWithValue("$expected", expectedRevision);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        // A revision-0 delete is an ensure-absent request and stays true when no row exists.
        return expectedRevision == 0 || affected == 1;
    }

    private async Task<TResult> RunBoundedAsync<TResult>(Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        return await operation(linked.Token).WaitAsync(linked.Token);
    }
}
