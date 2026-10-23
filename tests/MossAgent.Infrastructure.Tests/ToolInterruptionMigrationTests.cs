using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ToolInterruptionMigrationTests
{
    [Fact]
    public async Task ForwardMigrationRollsBackRetriesAndPreservesLegacyAuditsAndMessages()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var sink = new SqliteToolExecutionAuditSink(fixture.Connections);
        var id = await sink.StartAsync(new(fixture.Task.Id, "legacy-call", "browser.navigate",
            ToolRiskLevel.ExternalSideEffect, ApprovalPolicy.AskEveryTime, DateTimeOffset.UtcNow), fixture.Token);
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "preserved", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.ExecuteAsync("""
            DROP TABLE tool_execution_interruptions;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=24;
            CREATE TRIGGER reject_version_twenty_four BEFORE INSERT ON schema_versions
            WHEN NEW.version=24 BEGIN SELECT RAISE(ABORT,'synthetic audit migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(23, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='tool_execution_interruptions';"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM tool_executions;"));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_twenty_four;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(30, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var restored = Assert.Single(await new SqliteToolExecutionAuditReader(fixture.Connections).GetRecentAsync(fixture.Task.Id, 10, fixture.Token));
        Assert.Equal(id, restored.Id);
        Assert.Equal("legacy-call", restored.CallId);
        Assert.NotNull(restored.InterruptedAt);
        Assert.Null(restored.CompletedAt);
        Assert.Null(restored.IsSuccess);
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        await fixture.ExecuteAsync("DELETE FROM tool_executions;");
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM tool_execution_interruptions;"));
    }
}
