namespace MossAgent.Infrastructure.Persistence;

internal static class SteeringAttachmentDatabaseSchema
{
    public const string VersionThirtyNine = """
        CREATE TABLE IF NOT EXISTS conversation_steering_input_attachments (
            input_id TEXT NOT NULL REFERENCES conversation_steering_inputs(id) ON DELETE CASCADE,
            task_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal)='integer' AND ordinal BETWEEN 0 AND 9),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND
                ((media_type='text/plain' AND byte_count BETWEEN 0 AND 131072) OR
                 (media_type IN ('image/png','image/jpeg','image/gif','image/webp') AND byte_count BETWEEN 1 AND 5242880) OR
                 (media_type='application/pdf' AND byte_count BETWEEN 1 AND 10485760))),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            media_type TEXT NOT NULL DEFAULT 'text/plain',
            attachment_id TEXT NOT NULL UNIQUE,
            UNIQUE(input_id,ordinal)
        );
        CREATE INDEX IF NOT EXISTS ix_steering_input_attachments_task
            ON conversation_steering_input_attachments(task_id,input_id,ordinal);
        INSERT INTO schema_versions(version,applied_at) VALUES(39,datetime('now'));
        """;
}
