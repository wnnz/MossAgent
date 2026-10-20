namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationAttachmentDatabaseSchema
{
    public const string VersionFourteen = """
        CREATE TABLE agent_task_failures_v14 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15))
        );
        INSERT INTO agent_task_failures_v14(task_id,reason) SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v14 RENAME TO agent_task_failures;
        CREATE UNIQUE INDEX IF NOT EXISTS ix_messages_identity ON messages(id,task_id);
        CREATE TABLE IF NOT EXISTS conversation_attachments (
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL,
            message_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal) = 'integer' AND ordinal >= 0 AND ordinal < 10),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count) = 'integer' AND byte_count >= 0 AND byte_count <= 131072),
            sha256 TEXT NOT NULL CHECK(length(sha256) = 64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            UNIQUE(message_id,ordinal),
            FOREIGN KEY(message_id,task_id) REFERENCES messages(id,task_id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ix_conversation_attachments_task ON conversation_attachments(task_id,message_id,ordinal);
        INSERT OR IGNORE INTO schema_versions(version,applied_at) VALUES(14,datetime('now'));
        """;
}
