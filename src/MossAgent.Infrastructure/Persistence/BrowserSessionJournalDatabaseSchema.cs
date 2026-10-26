namespace MossAgent.Infrastructure.Persistence;

internal static class BrowserSessionJournalDatabaseSchema
{
    // Session rows store engine/profile-mode/state only: profile paths, CDP endpoints,
    // proxy credentials and page URLs never reach this table. Intent rows store the
    // truncated target hash only: raw selectors, typed text and URLs are never persisted.
    public const string VersionFortyThree = """
        CREATE TABLE IF NOT EXISTS browser_session_records (
            id TEXT PRIMARY KEY CHECK(length(id)=36),
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            engine INTEGER NOT NULL CHECK(engine IN (0,1,2)),
            profile_mode INTEGER NOT NULL CHECK(profile_mode IN (0,1,2)),
            state INTEGER NOT NULL CHECK(state IN (0,1,2,3)),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_browser_session_records_task
            ON browser_session_records(task_id,created_at);
        CREATE TABLE IF NOT EXISTS browser_action_intents (
            id TEXT PRIMARY KEY CHECK(length(id)=36),
            session_id TEXT NOT NULL REFERENCES browser_session_records(id) ON DELETE CASCADE,
            kind TEXT NOT NULL CHECK(kind IN ('navigate','navigate-back','navigate-forward','reload',
                'new-tab','close-tab','click','type','select-option','download')),
            target_hash TEXT NULL CHECK(target_hash IS NULL OR length(target_hash)=16),
            state INTEGER NOT NULL CHECK(state IN (0,1,2,3,4)),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_browser_action_intents_session
            ON browser_action_intents(session_id,created_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(43,datetime('now'));
        """;
}
