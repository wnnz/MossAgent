using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationPreferenceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("minimal")]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("xhigh")]
    [InlineData("max")]
    [InlineData("ultra")]
    public async Task Preference_RoundTripsWithoutChangingTaskMetadata(string? reasoning)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationPreferenceRepository(fixture.Connections);
        var preference = new ConversationModelPreference(Guid.NewGuid(), Guid.NewGuid(), reasoning, Guid.NewGuid());

        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.True(await repository.SaveAsync(fixture.Task.Id, preference, fixture.Token));
        await fixture.Repository.SaveTaskAsync(fixture.Task with { Title = "updated" }, fixture.Token);

        Assert.Equal(preference, await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(fixture.Task with { Title = "updated" }, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), fixture.Token));
    }

    [Fact]
    public async Task AgentDefaults_RoundTripSeparatelyFromAnExplicitReasoningOverride()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationPreferenceRepository(fixture.Connections);
        var preference = new ConversationModelPreference(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), true);

        Assert.True(await repository.SaveAsync(fixture.Task.Id, preference, fixture.Token));
        Assert.Equal(preference, await repository.GetAsync(fixture.Task.Id, fixture.Token));
        var explicitChoice = preference with { FollowAgentReasoning = false, ReasoningEffort = "high" };
        Assert.True(await repository.SaveAsync(fixture.Task.Id, explicitChoice, fixture.Token));
        Assert.Equal(explicitChoice, await repository.GetAsync(fixture.Task.Id, fixture.Token));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "high")]
    public async Task InvalidAgentDefaultSource_IsRejectedWithoutChangingThePreference(bool hasAgent, string? reasoning)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationPreferenceRepository(fixture.Connections);
        var preference = new ConversationModelPreference(Guid.NewGuid(), Guid.NewGuid(), reasoning,
            hasAgent ? Guid.NewGuid() : null, true);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(fixture.Task.Id, preference, fixture.Token));

        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task Save_RejectsMissingAndArchivedTasksWithoutOverwritingAnExistingChoice()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationPreferenceRepository(fixture.Connections);
        var preference = new ConversationModelPreference(Guid.NewGuid(), Guid.NewGuid(), "high");
        Assert.True(await repository.SaveAsync(fixture.Task.Id, preference, fixture.Token));
        Assert.False(await repository.SaveAsync(Guid.NewGuid(), preference, fixture.Token));
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, true, fixture.Token);

        Assert.False(await repository.SaveAsync(fixture.Task.Id, preference with { ModelProfileId = Guid.NewGuid() }, fixture.Token));
        Assert.Equal(preference, await repository.GetAsync(fixture.Task.Id, fixture.Token));
        Assert.Single(await fixture.Repository.GetArchivedTasksAsync(fixture.Project.Id, fixture.Token));
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("off")]
    [InlineData("")]
    public async Task InvalidReasoning_IsRejectedBeforeChangingThePreference(string reasoning)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationPreferenceRepository(fixture.Connections);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(fixture.Task.Id,
            new(Guid.NewGuid(), Guid.NewGuid(), reasoning), fixture.Token));

        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task Cancellation_DoesNotWriteAndStillRejectsARead()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationPreferenceRepository(fixture.Connections);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(fixture.Task.Id,
            new(Guid.NewGuid(), Guid.NewGuid()), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(fixture.Task.Id, cancellation.Token));
        Assert.Null(await repository.GetAsync(fixture.Task.Id, fixture.Token));
    }

    [Fact]
    public async Task VersionTenMigration_IsIdempotentAndPreservesHistory()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant, "preserved", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(fixture.Task.Id, true, fixture.Token);
        await fixture.ExecuteAsync("DROP TABLE conversation_model_preferences; DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=11;");

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(43, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Task with { IsPinned = true }, Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_model_preferences;"));
    }

    [Fact]
    public async Task FailedMigration_RollsBackTheTableAndPreservesHistoryBeforeRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_model_preferences;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=11;
            CREATE TRIGGER reject_version_eleven BEFORE INSERT ON schema_versions
            WHEN NEW.version=11 BEGIN SELECT RAISE(FAIL, 'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='conversation_model_preferences';"));
        Assert.Equal(10, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_eleven;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(43, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
    }
}
