namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationSteeringAttachmentDatabaseSchema
{
    /// <summary>
    /// Adds the snapshot metadata column to <c>conversation_steering_inputs</c>. Snapshots travel as a
    /// bounded JSON array because they are immutable metadata; the bytes live in the task attachment store.
    /// The statement is applied by <c>SqliteDatabaseInitializer</c> only while the column is missing, so a
    /// re-applied earlier steering version cannot leave the column absent.
    /// </summary>
    public const string AddAttachmentsColumn = """
        ALTER TABLE conversation_steering_inputs ADD COLUMN attachments TEXT NOT NULL DEFAULT '[]'
            CHECK(length(attachments) BETWEEN 2 AND 65536);
        """;
}
