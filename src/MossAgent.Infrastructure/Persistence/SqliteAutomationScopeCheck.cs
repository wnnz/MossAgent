using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

/// <summary>
/// Claim-time re-check of a plan's saved authorization snapshot.
/// The comparison and the blocked-run record run inside the caller's claim transaction, so a project
/// authorization change cannot be followed by a claim that still used the old roots.
/// </summary>
internal static class SqliteAutomationScopeCheck
{
    /// <summary>
    /// Error code stored on the blocked run; the UI maps it to a localized explanation.
    /// </summary>
    public const string AuthorizationChangedCode = "authorization-changed";

    /// <summary>
    /// Compares the plan's saved snapshot with the project row.
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Claim transaction.</param>
    /// <param name="schedule">Plan being claimed.</param>
    /// <param name="cancellationToken">Claim cancellation token.</param>
    /// <returns>
    /// True when the plan may run: fixed-snapshot plans must still match the project, follow-project
    /// plans and legacy plans without a snapshot are not compared.
    /// </returns>
    public static async Task<bool> MatchesAsync(SqliteConnection connection, SqliteTransaction transaction,
        AutomationSchedule schedule, CancellationToken cancellationToken)
    {
        if (AutomationScopeRules.PolicyOf(schedule) == AutomationScopePolicy.FollowProject) return true;
        var scope = schedule.Scope!;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM projects
            WHERE id=$project AND authorization_revision=$revision AND authorized_directories_json=$roots;
            """;
        command.Parameters.AddWithValue("$project", schedule.ProjectId.ToString());
        command.Parameters.AddWithValue("$revision", scope.ProjectRevision);
        command.Parameters.AddWithValue("$roots", JsonSerializer.Serialize(scope.AuthorizedRoots));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    /// <summary>
    /// Records one blocked attempt as a failed run and advances the schedule so the next try happens
    /// at the next interval instead of on every scheduler tick. Manual runs keep the schedule's
    /// original next-run time so the user's manual attempt does not shift the plan.
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Claim transaction.</param>
    /// <param name="schedule">Plan whose scope no longer matches.</param>
    /// <param name="now">Attempt time.</param>
    /// <param name="nextRunAt">Next run time to persist for scheduled attempts.</param>
    /// <param name="manual">Whether the attempt was requested manually.</param>
    /// <param name="cancellationToken">Claim cancellation token.</param>
    public static async Task RecordBlockedAsync(SqliteConnection connection, SqliteTransaction transaction,
        AutomationSchedule schedule, DateTimeOffset now, DateTimeOffset nextRunAt, bool manual,
        CancellationToken cancellationToken)
    {
        await using var advance = connection.CreateCommand();
        advance.Transaction = transaction;
        advance.CommandText = "UPDATE automation_schedules SET next_run_at=$next WHERE id=$id AND revision=$revision AND archived=0;";
        advance.Parameters.AddWithValue("$next", FormatTime(nextRunAt));
        advance.Parameters.AddWithValue("$id", schedule.Id.ToString());
        advance.Parameters.AddWithValue("$revision", schedule.Revision);
        if (await advance.ExecuteNonQueryAsync(cancellationToken) != 1) return;
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO automation_runs(id,schedule_id,task_id,started_at,status,manual,finished_at,error_code)
            VALUES($id,$schedule,$task,$started,$status,$manual,$finished,$error);
            """;
        insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        insert.Parameters.AddWithValue("$schedule", schedule.Id.ToString());
        insert.Parameters.AddWithValue("$task", Guid.NewGuid().ToString());
        insert.Parameters.AddWithValue("$started", FormatTime(now));
        insert.Parameters.AddWithValue("$status", (int)AutomationRunStatus.Failed);
        insert.Parameters.AddWithValue("$manual", manual);
        insert.Parameters.AddWithValue("$finished", FormatTime(now));
        insert.Parameters.AddWithValue("$error", AuthorizationChangedCode);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}
