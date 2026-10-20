namespace MossAgent.Infrastructure.Persistence;

internal static class SourceReviewChatDatabaseSchema
{
    public const string VersionTwenty = """
        CREATE TABLE IF NOT EXISTS source_review_chats(
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            grant_id TEXT NOT NULL, account_id TEXT NOT NULL,
            account_revision INTEGER NOT NULL CHECK(account_revision > 0),
            owner TEXT NOT NULL, repository TEXT NOT NULL,
            number INTEGER NOT NULL CHECK(number > 0), base_revision TEXT NOT NULL COLLATE NOCASE, head_revision TEXT NOT NULL COLLATE NOCASE,
            context TEXT NOT NULL CHECK(length(context) BETWEEN 1 AND 24576),
            UNIQUE(grant_id,number,base_revision,head_revision)
        );
        CREATE TRIGGER IF NOT EXISTS source_review_chat_environment BEFORE UPDATE ON agent_tasks
        WHEN EXISTS(SELECT 1 FROM source_review_chats WHERE task_id=OLD.id)
          AND (NEW.project_id IS NOT NULL OR NEW.worktree_path IS NOT NULL OR NEW.approval_policy<>0)
        BEGIN SELECT RAISE(ABORT,'Source review chats cannot acquire a local environment.'); END;
        INSERT INTO schema_versions(version,applied_at) VALUES(20,datetime('now'));
        """;
}
