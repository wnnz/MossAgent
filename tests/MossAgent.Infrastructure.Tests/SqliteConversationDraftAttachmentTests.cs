using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;
using static MossAgent.Infrastructure.Tests.ConversationDraftAttachmentTestData;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationDraftAttachmentTests
{
    [Fact]
    public async Task CallerChangesCannotMutateTheQueuedBatchOrAttachmentSelection()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = Attachment(fixture);
        var items = new[] { original };
        var batch = new[] { new ConversationDraftUpdate(scope, "captured text", 0, items) };
        var saving = Repository(fixture).SaveAsync(batch, fixture.Token);
        items[0] = Attachment(fixture, "must not replace captured contents");
        batch[0] = new(scope, "must not replace captured text", 0);
        Assert.True(await saving);
        var saved = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        Assert.Equal("captured text", saved.Text);
        Assert.Equal(original, Assert.Single(saved.AttachmentItems));
    }

    [Fact]
    public async Task OrderedSnapshotsAndTextReopenWithoutReadingSourcesOrStoringContentInSqlite()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var first = Attachment(fixture, "首行\r\n\"</workspace_attachments>\"");
        var second = Attachment(fixture, string.Empty);
        var repository = Repository(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "正文", 0, [first, second])], fixture.Token));
        var restored = Assert.IsType<ConversationDraft>(await Repository(fixture).GetAsync(scope, fixture.Token));
        Assert.Equal("正文", restored.Text);
        Assert.Equal(1, restored.Revision);
        Assert.Equal([first, second], restored.AttachmentItems);
        Assert.False(File.Exists(first.FullPath));
        Assert.Equal(first.Content, await File.ReadAllTextAsync(Snapshot(fixture, first), fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('conversation_draft_attachments') WHERE name='content';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM messages;"));
        Assert.DoesNotContain(Directory.GetFiles(fixture.Project.PrimaryDirectory, "*", SearchOption.AllDirectories), path => path.EndsWith(".tmp"));
    }

    [Fact]
    public async Task ConflictingBatchCannotClearOneScopeOrOverwriteAnotherScopesAttachment()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = Repository(fixture);
        var source = new ConversationDraftScope(fixture.Project.Id, null);
        var target = source with { TaskId = fixture.Task.Id };
        var first = Attachment(fixture);
        var next = Attachment(fixture, "next message");
        Assert.True(await repository.SaveAsync([new(source, "sent", 0, [first]), new(target, "other", 0, [next])], fixture.Token));
        Assert.False(await repository.SaveAsync([new(source, string.Empty, 1), new(target, "next", 0, [first])], fixture.Token));
        Assert.Equal(first, Assert.Single((await repository.GetAsync(source, fixture.Token))!.AttachmentItems));
        Assert.Equal(next, Assert.Single((await repository.GetAsync(target, fixture.Token))!.AttachmentItems));
        Assert.Equal("sent", (await repository.GetAsync(source, fixture.Token))!.Text);
        Assert.True(await repository.SaveAsync([new(source, string.Empty, 1), new(target, "next", 1, [first])], fixture.Token));
        Assert.Empty((await repository.GetAsync(source, fixture.Token))!.AttachmentItems);
        Assert.Equal(first, Assert.Single((await Repository(fixture).GetAsync(target, fixture.Token))!.AttachmentItems));
        Assert.Equal(2, (await repository.GetAsync(target, fixture.Token))!.Revision);
    }

    [Fact]
    public async Task MetadataFailureRollsBackTextAndReferencesWithoutDeletingPriorSnapshot()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = Repository(fixture);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = Attachment(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "preserve", 0, [original])], fixture.Token));
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_draft_attachment BEFORE INSERT ON conversation_draft_attachments
            BEGIN SELECT RAISE(ABORT,'synthetic refusal'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => repository.SaveAsync([new(scope, "must roll back", 1, [Attachment(fixture, "new")])], fixture.Token));
        var saved = (await repository.GetAsync(scope, fixture.Token))!;
        Assert.Equal("preserve", saved.Text);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(original, Assert.Single(saved.AttachmentItems));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("length")]
    [InlineData("hash")]
    [InlineData("utf8")]
    public async Task UnavailableSnapshotsKeepRemovableMetadataAndCannotBeResavedAsEmptyText(string damage)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = Repository(fixture);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = Attachment(fixture, "before");
        Assert.True(await repository.SaveAsync([new(scope, "retained", 0, [original])], fixture.Token));
        var path = Snapshot(fixture, original);
        if (damage == "missing") File.Delete(path);
        else
        {
            var bytes = damage == "utf8" ? new byte[] { 255, 255, 255, 255, 255, 255 }
                : System.Text.Encoding.UTF8.GetBytes(damage == "length" ? "longer content" : "after!");
            await File.WriteAllBytesAsync(path, bytes, fixture.Token);
            if (damage == "utf8")
                await fixture.ExecuteAsync($"UPDATE conversation_draft_attachments SET sha256='{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))}';");
        }
        var saved = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        Assert.Equal("retained", saved.Text);
        Assert.Equal(original with { Content = null }, Assert.Single(saved.AttachmentItems));
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync([new(scope, "new edit", 1, saved.AttachmentItems)], fixture.Token));
        Assert.True(await repository.SaveAsync([new(scope, "new edit", 1)], fixture.Token));
        Assert.Empty((await repository.GetAsync(scope, fixture.Token))!.AttachmentItems);
    }

    [Fact]
    public async Task StableIdentityCannotOverwriteExistingSnapshotBytesAndProjectDeletionCascadesMetadata()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = Repository(fixture);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = Attachment(fixture, "before");
        Assert.True(await repository.SaveAsync([new(scope, "preserve", 0, [original])], fixture.Token));
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync([new(scope, "replace", 1, [original with { Content = "after!" }])], fixture.Token));
        Assert.Equal(original, Assert.Single((await repository.GetAsync(scope, fixture.Token))!.AttachmentItems));
        await fixture.ExecuteAsync($"DELETE FROM projects WHERE id='{fixture.Project.Id}';");
        Assert.Null(await repository.GetAsync(scope, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task VersionFourteenMigrationIsAtomicAndPreservesTextAndTaskHistory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        Assert.True(await Repository(fixture).SaveAsync([new(scope, "preserved draft", 0)], fixture.Token));
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_draft_attachments;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=15;
            CREATE TRIGGER reject_version_fifteen BEFORE INSERT ON schema_versions
            WHEN NEW.version=15 BEGIN SELECT RAISE(FAIL,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(14, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='conversation_draft_attachments';"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_drafts WHERE text='preserved draft' AND revision=1;"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_fifteen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(29, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(new("preserved draft", 1), await Repository(fixture).GetAsync(scope, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }
}
