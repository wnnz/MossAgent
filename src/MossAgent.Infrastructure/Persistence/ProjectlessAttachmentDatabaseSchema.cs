namespace MossAgent.Infrastructure.Persistence;

internal static class ProjectlessAttachmentDatabaseSchema
{
    public const string VersionSeventeen = """
        DROP TRIGGER validate_draft_attachment_scope;
        CREATE TRIGGER validate_draft_attachment_scope BEFORE INSERT ON conversation_draft_attachments
        WHEN NOT EXISTS(SELECT 1 FROM conversation_drafts WHERE scope_key=NEW.scope_key)
        BEGIN SELECT RAISE(ABORT,'Invalid draft attachment scope.'); END;
        INSERT INTO schema_versions(version,applied_at) VALUES(17,datetime('now'));
        """;
}
