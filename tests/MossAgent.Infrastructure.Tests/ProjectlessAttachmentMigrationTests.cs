using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectlessAttachmentMigrationTests
{
    [Fact]
    public async Task VersionSixteenFailureRollsBackScopeTriggerAndPreservesSnapshotsUntilSuccessfulRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var projectScope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var repository = ConversationDraftAttachmentTestData.Repository(fixture);
        var original = ConversationDraftAttachmentTestData.Attachment(fixture, "preserved snapshot");
        Assert.True(await repository.SaveAsync([new(projectScope, "preserved draft", 0, [original])], fixture.Token));
        await RestoreVersionSixteenAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_version_seventeen BEFORE INSERT ON schema_versions
            WHEN NEW.version=17 BEGIN SELECT RAISE(FAIL,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(16, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var plainScope = new ConversationDraftScope(null, null);
        var selected = original with { AuthorizationStamp = WorkspaceAttachmentAuthorization.CreateSelection(original.FullPath) };
        await Assert.ThrowsAsync<SqliteException>(() => repository.SaveAsync([new(plainScope, "must roll back", 0, [selected])], fixture.Token));
        Assert.Null(await repository.GetAsync(plainScope, fixture.Token));
        Assert.Equal("preserved draft", (await repository.GetAsync(projectScope, fixture.Token))!.Text);
        Assert.Equal(original, Assert.Single((await repository.GetAsync(projectScope, fixture.Token))!.AttachmentItems));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_seventeen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(37, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.True(await repository.SaveAsync([new(plainScope, "explicit attachment", 0, [selected])], fixture.Token));
        Assert.Equal(selected, Assert.Single((await repository.GetAsync(plainScope, fixture.Token))!.AttachmentItems));
        Assert.Equal(original, Assert.Single((await repository.GetAsync(projectScope, fixture.Token))!.AttachmentItems));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ProjectRootStampsCannotAuthorizeProjectlessDraftAttachments()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = ConversationDraftAttachmentTestData.Repository(fixture);
        var scope = new ConversationDraftScope(null, null);
        var attachment = ConversationDraftAttachmentTestData.Attachment(fixture);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SaveAsync([new(scope, "must not save", 0, [attachment])], fixture.Token));
        Assert.Null(await repository.GetAsync(scope, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
    }

    private static Task RestoreVersionSixteenAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=17;
        DROP TRIGGER validate_draft_attachment_scope;
        CREATE TRIGGER validate_draft_attachment_scope BEFORE INSERT ON conversation_draft_attachments
        WHEN NOT EXISTS(SELECT 1 FROM conversation_drafts WHERE scope_key=NEW.scope_key AND project_id IS NOT NULL)
        BEGIN SELECT RAISE(ABORT,'Invalid draft attachment scope.'); END;
        """);
}
