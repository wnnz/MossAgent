namespace MossAgent.Infrastructure.Persistence;

internal static class DocumentAttachmentDatabaseSchema
{
    public const string VersionThirtyTwo = """
        CREATE TABLE conversation_attachments_v32 (
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL,
            message_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal)='integer' AND ordinal BETWEEN 0 AND 9),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND
                ((media_type='text/plain' AND byte_count BETWEEN 0 AND 131072) OR
                 (media_type IN ('image/png','image/jpeg','image/gif','image/webp') AND byte_count BETWEEN 1 AND 5242880) OR
                 (media_type='application/pdf' AND byte_count BETWEEN 1 AND 10485760))),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            media_type TEXT NOT NULL DEFAULT 'text/plain',
            UNIQUE(message_id,ordinal),
            FOREIGN KEY(message_id,task_id) REFERENCES messages(id,task_id) ON DELETE CASCADE
        );
        INSERT INTO conversation_attachments_v32(id,task_id,message_id,ordinal,display_path,byte_count,sha256,media_type)
            SELECT id,task_id,message_id,ordinal,display_path,byte_count,sha256,media_type FROM conversation_attachments;
        DROP TABLE conversation_attachments;
        ALTER TABLE conversation_attachments_v32 RENAME TO conversation_attachments;
        CREATE INDEX ix_conversation_attachments_task ON conversation_attachments(task_id,message_id,ordinal);
        CREATE TABLE conversation_draft_attachments_v32 (
            scope_key TEXT NOT NULL REFERENCES conversation_drafts(scope_key) ON DELETE CASCADE,
            attachment_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal)='integer' AND ordinal BETWEEN 0 AND 9),
            source_path TEXT NOT NULL CHECK(length(source_path) BETWEEN 1 AND 32768),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND
                ((media_type='text/plain' AND byte_count BETWEEN 0 AND 131072) OR
                 (media_type IN ('image/png','image/jpeg','image/gif','image/webp') AND byte_count BETWEEN 1 AND 5242880) OR
                 (media_type='application/pdf' AND byte_count BETWEEN 1 AND 10485760))),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            authorization_stamp TEXT NOT NULL CHECK(length(authorization_stamp)=64 AND authorization_stamp NOT GLOB '*[^0-9A-Fa-f]*'),
            media_type TEXT NOT NULL DEFAULT 'text/plain',
            PRIMARY KEY(scope_key,attachment_id),
            UNIQUE(scope_key,ordinal)
        );
        INSERT INTO conversation_draft_attachments_v32(scope_key,attachment_id,ordinal,source_path,display_path,byte_count,sha256,authorization_stamp,media_type)
            SELECT scope_key,attachment_id,ordinal,source_path,display_path,byte_count,sha256,authorization_stamp,media_type FROM conversation_draft_attachments;
        DROP TABLE conversation_draft_attachments;
        ALTER TABLE conversation_draft_attachments_v32 RENAME TO conversation_draft_attachments;
        CREATE TRIGGER validate_draft_attachment_scope BEFORE INSERT ON conversation_draft_attachments
        WHEN NOT EXISTS(SELECT 1 FROM conversation_drafts WHERE scope_key=NEW.scope_key)
        BEGIN SELECT RAISE(ABORT,'Invalid draft attachment scope.'); END;
        INSERT INTO schema_versions(version,applied_at) VALUES(32,datetime('now'));
        """;
}
