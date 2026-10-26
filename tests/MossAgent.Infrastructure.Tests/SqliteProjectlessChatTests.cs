using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteProjectlessChatTests
{
    [Fact]
    public async Task ChatWithoutProject_RoundTripsRenamesPinsArchivesAndRecovers()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var chat = fixture.Task with { Id = Guid.NewGuid(), ProjectId = null, Status = AgentTaskStatus.Running };
        var message = new ConversationMessage(Guid.NewGuid(), chat.Id, MessageRole.User, "plain chat", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(chat, [message], fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var recovered = Assert.Single(await fixture.Repository.GetTasksAsync(null, fixture.Token));
        Assert.Equal(AgentTaskStatus.Interrupted, recovered.Status);
        Assert.Null(recovered.ProjectId);
        Assert.Null(recovered.WorktreePath);
        Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        var renamed = await fixture.Repository.RenameTaskAsync(recovered, "renamed chat", fixture.Token);
        Assert.NotNull(renamed);
        Assert.Equal("renamed chat", renamed.Title);
        Assert.Null(await fixture.Repository.RenameTaskAsync(renamed with { ProjectId = fixture.Project.Id }, "wrong scope", fixture.Token));
        Assert.True(await fixture.Repository.SetTaskPinnedAsync(chat.Id, true, fixture.Token));
        Assert.Null(Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token)).ProjectId);
        await fixture.Repository.SetTaskArchivedAsync(chat.Id, true, fixture.Token);
        Assert.Empty(await fixture.Repository.GetTasksAsync(null, fixture.Token));
        Assert.Single(await fixture.Repository.GetArchivedTasksAsync(null, fixture.Token));
        Assert.Empty(await fixture.Repository.GetArchivedTasksAsync(fixture.Project.Id, fixture.Token));
        await fixture.Repository.SetTaskArchivedAsync(chat.Id, false, fixture.Token);
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(chat.Id, fixture.Token)));
    }

    [Fact]
    public async Task InvalidScopeOrProjectlessWorktree_IsRejectedAtomically()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var chat = fixture.Task with { Id = Guid.NewGuid(), ProjectId = null };
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskAsync(
            chat with { WorktreePath = "D:\\unauthorized" }, fixture.Token));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskAsync(
            chat with { ProjectId = Guid.NewGuid() }, fixture.Token));
        Assert.Empty(await fixture.Repository.GetTasksAsync(null, fixture.Token));
        await fixture.Repository.SaveTaskAsync(chat, fixture.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.SaveTaskWithMessagesAsync(
            fixture.Task with { ProjectId = null, Title = "cross-scope overwrite" },
            [new(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "must roll back", DateTimeOffset.UtcNow)], fixture.Token));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Fact]
    public async Task VersionElevenUpgrade_PreservesAllDependentsAndCascadeRelationships()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await SeedDependentsAsync(fixture);
        await RestoreVersionElevenAsync(fixture);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(41, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        foreach (var table in DependentTables)
            Assert.Equal(1, await fixture.CountAsync($"SELECT COUNT(*) FROM {table};"));
        Assert.Equal(1, await fixture.CountAsync("PRAGMA foreign_keys;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
        var chat = fixture.Task with { Id = Guid.NewGuid(), ProjectId = null };
        await fixture.Repository.SaveTaskAsync(chat, fixture.Token);
        await fixture.ExecuteAsync($"DELETE FROM projects WHERE id='{fixture.Project.Id}';");
        foreach (var table in DependentTables)
            Assert.Equal(0, await fixture.CountAsync($"SELECT COUNT(*) FROM {table};"));
        Assert.Equal(chat, Assert.Single(await fixture.Repository.GetTasksAsync(null, fixture.Token)));
    }

    [Fact]
    public async Task FailedMigration_RollsBackParentAndChildrenAndCanRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await SeedDependentsAsync(fixture);
        await RestoreVersionElevenAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_version_twelve BEFORE INSERT ON schema_versions
            WHEN NEW.version=12 BEGIN SELECT RAISE(FAIL, 'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=12;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='agent_tasks_projectless';"));
        Assert.Equal(1, await fixture.CountAsync("SELECT [notnull] FROM pragma_table_info('agent_tasks') WHERE name='project_id';"));
        foreach (var table in DependentTables)
            Assert.Equal(1, await fixture.CountAsync($"SELECT COUNT(*) FROM {table};"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskAsync(
            fixture.Task with { Id = Guid.NewGuid(), ProjectId = null }, fixture.Token));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_twelve;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Id = Guid.NewGuid(), ProjectId = null }, fixture.Token);
        Assert.Single(await fixture.Repository.GetTasksAsync(null, fixture.Token));
    }

    [Fact]
    public async Task BrokenForeignKeys_AbortMigrationWithoutReinterpretingData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await RestoreVersionElevenAsync(fixture);
        await fixture.ExecuteAsync($"""
            PRAGMA foreign_keys=OFF;
            INSERT INTO messages VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}',0,'orphan',datetime('now'),NULL);
            PRAGMA foreign_keys=ON;
            """);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=12;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM messages;"));
        Assert.Equal(1, await fixture.CountAsync("PRAGMA foreign_keys;"));
    }

    private static readonly string[] DependentTables = ["messages", "tool_executions", "tool_artifacts",
        "pinned_agent_tasks", "archived_agent_tasks", "agent_task_failures", "conversation_model_preferences"];

    private static async Task SeedDependentsAsync(SqliteFailurePersistenceFixture fixture)
    {
        await fixture.Repository.SaveTaskAsync(fixture.Task with { FailureReason = TaskFailureReason.ProviderHttp }, fixture.Token);
        await fixture.Repository.AppendMessageAsync(new(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant,
            "preserved history", DateTimeOffset.UtcNow), fixture.Token);
        Assert.True(await fixture.Repository.SetTaskPinnedAsync(fixture.Task.Id, true, fixture.Token));
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, true, fixture.Token);
        var executionId = Guid.NewGuid();
        await fixture.ExecuteAsync($"""
            INSERT INTO tool_executions(id,task_id,call_id,tool_name,approval_policy,started_at)
                VALUES('{executionId}','{fixture.Task.Id}','synthetic-call','file.read',0,datetime('now'));
            INSERT INTO tool_artifacts VALUES('{Guid.NewGuid()}','{executionId}','{fixture.Task.Id}',
                'fixture','synthetic.txt','text/plain',0,'synthetic-hash',datetime('now'));
            INSERT INTO conversation_model_preferences(task_id,provider_id,model_profile_id,reasoning_effort)
                VALUES('{fixture.Task.Id}','{Guid.NewGuid()}','{Guid.NewGuid()}','medium');
            """);
    }

    private static Task RestoreVersionElevenAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        PRAGMA foreign_keys=OFF;
        BEGIN;
        CREATE TABLE agent_tasks_v11 (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            title TEXT NOT NULL,approval_policy INTEGER NOT NULL,status INTEGER NOT NULL,
            created_at TEXT NOT NULL,updated_at TEXT NOT NULL,worktree_path TEXT NULL
        );
        INSERT INTO agent_tasks_v11 SELECT * FROM agent_tasks;
        DROP TABLE agent_tasks;
        ALTER TABLE agent_tasks_v11 RENAME TO agent_tasks;
        DELETE FROM schema_versions WHERE version=12;
        COMMIT;
        PRAGMA foreign_keys=ON;
        """);
}
