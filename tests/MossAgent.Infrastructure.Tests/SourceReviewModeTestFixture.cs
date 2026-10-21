using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Tests;

internal sealed class SourceReviewModeTestFixture(SqliteFailurePersistenceFixture storage, SourceReviewChatBinding binding) : IAsyncDisposable
{
    public SqliteFailurePersistenceFixture Storage { get; } = storage;
    public SourceReviewChatBinding Binding { get; } = binding;
    public SqliteSourceReviewChats Chats { get; } = new(storage.Connections);
    public CancellationToken Token => Storage.Token;

    public static async Task<SourceReviewModeTestFixture> CreateAsync()
    {
        var storage = await SqliteFailurePersistenceFixture.CreateAsync();
        try
        {
            var account = await new SqliteSourceAccountRepository(storage.Connections).SaveAsync(SourceAccountValidationTests.Create(), storage.Token);
            var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
            await new SqliteSourceRepositoryGrants(storage.Connections).AddAsync(scope, storage.Token);
            return new(storage, new(Guid.NewGuid(), scope, new(7, new string('a', 40), new string('b', 40)), "legacy snapshot"));
        }
        catch { await storage.DisposeAsync(); throw; }
    }

    public Task RestoreVersion21Async() => Storage.ExecuteAsync("""
        DROP TRIGGER source_review_chat_environment;
        CREATE TABLE source_review_chats_v21(
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            grant_id TEXT NOT NULL, account_id TEXT NOT NULL,
            account_revision INTEGER NOT NULL CHECK(account_revision > 0),
            owner TEXT NOT NULL, repository TEXT NOT NULL,
            number INTEGER NOT NULL CHECK(number > 0), base_revision TEXT NOT NULL COLLATE NOCASE, head_revision TEXT NOT NULL COLLATE NOCASE,
            context TEXT NOT NULL CHECK(length(context) BETWEEN 1 AND 24576),
            UNIQUE(grant_id,number,base_revision,head_revision)
        );
        INSERT INTO source_review_chats_v21 SELECT task_id,grant_id,account_id,account_revision,owner,repository,number,base_revision,head_revision,context FROM source_review_chats;
        DROP TABLE source_review_chats;
        ALTER TABLE source_review_chats_v21 RENAME TO source_review_chats;
        CREATE TRIGGER source_review_chat_environment BEFORE UPDATE ON agent_tasks
        WHEN EXISTS(SELECT 1 FROM source_review_chats WHERE task_id=OLD.id)
          AND (NEW.project_id IS NOT NULL OR NEW.worktree_path IS NOT NULL OR NEW.approval_policy<>0)
        BEGIN SELECT RAISE(ABORT,'Source review chats cannot acquire a local environment.'); END;
        DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=22;
        """);

    public ValueTask DisposeAsync() => Storage.DisposeAsync();
}
