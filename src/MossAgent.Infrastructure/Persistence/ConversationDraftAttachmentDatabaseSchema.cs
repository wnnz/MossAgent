namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationDraftAttachmentDatabaseSchema
{
    public const string VersionFifteen = """
        CREATE TABLE IF NOT EXISTS conversation_draft_attachments (
            scope_key TEXT NOT NULL REFERENCES conversation_drafts(scope_key) ON DELETE CASCADE,
            attachment_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal)='integer' AND ordinal BETWEEN 0 AND 9),
            source_path TEXT NOT NULL CHECK(length(source_path) BETWEEN 1 AND 32768),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND byte_count BETWEEN 0 AND 131072),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            authorization_stamp TEXT NOT NULL CHECK(length(authorization_stamp)=64 AND authorization_stamp NOT GLOB '*[^0-9A-Fa-f]*'),
            PRIMARY KEY(scope_key,attachment_id),
            UNIQUE(scope_key,ordinal)
        );
        CREATE TRIGGER IF NOT EXISTS validate_draft_attachment_scope BEFORE INSERT ON conversation_draft_attachments
        WHEN NOT EXISTS (SELECT 1 FROM conversation_drafts WHERE scope_key=NEW.scope_key AND project_id IS NOT NULL)
        BEGIN SELECT RAISE(ABORT,'Invalid draft attachment scope.'); END;
        INSERT OR IGNORE INTO schema_versions(version,applied_at) VALUES(15,datetime('now'));
        """;
}
