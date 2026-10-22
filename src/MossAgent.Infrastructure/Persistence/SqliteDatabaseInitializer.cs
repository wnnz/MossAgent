using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer(
    AppDataPaths paths,
    SqliteConnectionFactory connections) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqliteCommand(DatabaseSchema.VersionOne, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var versionTwo = new SqliteCommand(DatabaseSchema.VersionTwo, connection);
        await versionTwo.ExecuteNonQueryAsync(cancellationToken);
        await using var versionThree = new SqliteCommand(DatabaseSchema.VersionThree, connection);
        await versionThree.ExecuteNonQueryAsync(cancellationToken);
        await using var versionFour = new SqliteCommand(DatabaseSchema.VersionFour, connection);
        await versionFour.ExecuteNonQueryAsync(cancellationToken);
        await using var versionFive = new SqliteCommand(DatabaseSchema.VersionFive, connection);
        await versionFive.ExecuteNonQueryAsync(cancellationToken);
        await using var versionSix = new SqliteCommand(DatabaseSchema.VersionSix, connection);
        await versionSix.ExecuteNonQueryAsync(cancellationToken);
        await using var versionSeven = new SqliteCommand(DatabaseSchema.VersionSeven, connection);
        await versionSeven.ExecuteNonQueryAsync(cancellationToken);
        await ApplyVersionAsync(connection, 8, DatabaseSchema.VersionEight, cancellationToken);
        await ApplyVersionNineAsync(connection, cancellationToken);
        await ApplyVersionAsync(connection, 10, TaskFailureDatabaseSchema.VersionTen, cancellationToken);
        await ApplyVersionAsync(connection, 11, ConversationPreferenceDatabaseSchema.VersionEleven, cancellationToken);
        await ProjectlessChatMigration.ApplyAsync(connection, cancellationToken);
        await ApplyVersionAsync(connection, 13, ConversationDraftDatabaseSchema.VersionThirteen, cancellationToken);
        await ApplyVersionAsync(connection, 14, ConversationAttachmentDatabaseSchema.VersionFourteen, cancellationToken);
        await ApplyVersionAsync(connection, 15, ConversationDraftAttachmentDatabaseSchema.VersionFifteen, cancellationToken);
        await ApplyVersionAsync(connection, 16, ImageAttachmentDatabaseSchema.VersionSixteen, cancellationToken);
        await ApplyVersionAsync(connection, 17, ProjectlessAttachmentDatabaseSchema.VersionSeventeen, cancellationToken);
        await ApplyVersionAsync(connection, 18, SourceAccountDatabaseSchema.VersionEighteen, cancellationToken);
        await ApplyVersionAsync(connection, 19, SourceRepositoryDatabaseSchema.VersionNineteen, cancellationToken);
        await ApplyVersionAsync(connection, 20, SourceReviewChatDatabaseSchema.VersionTwenty, cancellationToken);
        await ApplyVersionAsync(connection, 21, ModelContextFailureDatabaseSchema.VersionTwentyOne, cancellationToken);
        await ApplyVersionAsync(connection, 22, SourceReviewReadModeDatabaseSchema.VersionTwentyTwo, cancellationToken);
        await ApplyVersionAsync(connection, 23, ConversationFollowupDatabaseSchema.VersionTwentyThree, cancellationToken);
        await ApplyVersionAsync(connection, 24, ToolInterruptionDatabaseSchema.VersionTwentyFour, cancellationToken);
        await ApplyVersionAsync(connection, 25, ConversationSteeringDatabaseSchema.VersionTwentyFive, cancellationToken);
        await ApplyVersionAsync(connection, 26, ProjectAuthorizationDatabaseSchema.VersionTwentySix, cancellationToken);
        await ApplyVersionAsync(connection, 27, RootObjectAuthorizationDatabaseSchema.VersionTwentySeven, cancellationToken);
        await ApplyVersionAsync(connection, 28, WorktreeRootAuthorizationDatabaseSchema.VersionTwentyEight, cancellationToken);
        await ApplyVersionTwentyNineAsync(connection, cancellationToken);
        await ApplyVersionAsync(connection, 30, AuthorizationResourceRegistryDatabaseSchema.VersionThirty, cancellationToken);
        await ApplyVersionAsync(connection, 31, WorktreeCreationIntentDatabaseSchema.VersionThirtyOne, cancellationToken);
        await SqliteWorktreeCreationIntentStore.ReconcileStartupAsync(connection, cancellationToken);
        await SqliteInterruptedWorkRecovery.RecoverAsync(connection, cancellationToken);
    }

    private static async Task ApplyVersionNineAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = AutomationDatabaseSchema.VersionNine;
        await migration.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Applies the steering attachment column and its version marker idempotently.
    /// The column is added whenever it is still missing, so re-applying the earlier steering version
    /// (which recreates the input table without the column) cannot leave reads without snapshot metadata.
    /// </summary>
    private static async Task ApplyVersionTwentyNineAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var table = connection.CreateCommand();
        table.Transaction = transaction;
        table.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='conversation_steering_inputs';";
        if (Convert.ToInt64(await table.ExecuteScalarAsync(cancellationToken)) == 1)
        {
            await using var column = connection.CreateCommand();
            column.Transaction = transaction;
            column.CommandText = "SELECT COUNT(*) FROM pragma_table_info('conversation_steering_inputs') WHERE name='attachments';";
            if (Convert.ToInt64(await column.ExecuteScalarAsync(cancellationToken)) == 0)
            {
                await using var alter = connection.CreateCommand();
                alter.Transaction = transaction;
                alter.CommandText = ConversationSteeringAttachmentDatabaseSchema.AddAttachmentsColumn;
                await alter.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await using var marker = connection.CreateCommand();
        marker.Transaction = transaction;
        marker.CommandText = "INSERT OR IGNORE INTO schema_versions(version,applied_at) VALUES(29,datetime('now'));";
        await marker.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ApplyVersionAsync(SqliteConnection connection, int version, string schema,
        CancellationToken cancellationToken)
    {
        await using var transaction = connection.BeginTransaction();
        await using var existing = connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=$version;";
        existing.Parameters.AddWithValue("$version", version);
        if (Convert.ToInt64(await existing.ExecuteScalarAsync(cancellationToken)) != 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        await using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = schema;
        await migration.ExecuteNonQueryAsync(cancellationToken);
        if (Convert.ToInt64(await existing.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidOperationException("Workspace migration did not persist its version.");
        await transaction.CommitAsync(cancellationToken);
    }
}
