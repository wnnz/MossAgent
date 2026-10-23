using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteTaskFailureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public async Task FailureCategories_RoundTripAcrossAllCatalogs(int category)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = (TaskFailureReason)category };
        await fixture.Repository.SaveTaskAsync(failed, fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(failed.Id, true, fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(failed with { IsPinned = true }, Assert.Single(await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token)));
        Assert.Equal((TaskFailureReason)category, Assert.Single(await fixture.Repository.GetPinnedTasksAsync(fixture.Token)).FailureReason);
        await fixture.Repository.SetTaskArchivedAsync(failed.Id, true, fixture.Token);
        Assert.Empty(await fixture.Repository.GetPinnedTasksAsync(fixture.Token));
        Assert.Equal((TaskFailureReason)category, Assert.Single(await fixture.Repository.GetArchivedTasksAsync(failed.ProjectId, fixture.Token)).FailureReason);
        await fixture.Repository.SetTaskArchivedAsync(failed.Id, false, fixture.Token);
        Assert.Equal((TaskFailureReason)category, Assert.Single(await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token)).FailureReason);
        Assert.Equal(category, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }

    [Theory]
    [InlineData(AgentTaskStatus.Draft)]
    [InlineData(AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.WaitingForApproval)]
    [InlineData(AgentTaskStatus.Completed)]
    [InlineData(AgentTaskStatus.Cancelled)]
    [InlineData(AgentTaskStatus.Interrupted)]
    public async Task NonFailedUpdates_RemoveOnlyTheirOwnDiagnostic(AgentTaskStatus status)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.StreamLineLimit };
        var other = failed with { Id = Guid.NewGuid(), FailureReason = TaskFailureReason.ProviderAuthentication };
        await fixture.Repository.SaveTaskAsync(failed, fixture.Token);
        await fixture.Repository.SaveTaskAsync(other, fixture.Token);
        await fixture.Repository.SaveTaskAsync(failed with { Status = status }, fixture.Token);
        var tasks = await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token);
        Assert.Null(Assert.Single(tasks, task => task.Id == failed.Id).FailureReason);
        Assert.Equal(TaskFailureReason.ProviderAuthentication, Assert.Single(tasks, task => task.Id == other.Id).FailureReason);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM agent_task_failures;"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvalidCallerCategories_AreNormalizedWithoutArbitraryMetadata(int category)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.Repository.SaveTaskAsync(fixture.Task with { FailureReason = (TaskFailureReason)category }, fixture.Token);
        Assert.Equal(TaskFailureReason.Unknown, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).FailureReason);
        Assert.Equal(0, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("18")]
    [InlineData("14.5")]
    [InlineData("'Bearer synthetic-secret'")]
    [InlineData("NULL")]
    public async Task DatabaseConstraint_RejectsNonAllowlistedMetadata(string value)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO agent_task_failures(task_id,reason) VALUES('{fixture.Task.Id}',{value});"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM agent_task_failures;"));
    }

    [Fact]
    public async Task VersionSixMigration_IsIdempotentAndPreservesLegacyData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant, "legacy reply", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.Repository.SetTaskPinnedAsync(fixture.Task.Id, true, fixture.Token);
        var archived = fixture.Task with { Id = Guid.NewGuid(), Title = "archived", Status = AgentTaskStatus.Completed };
        await fixture.Repository.SaveTaskAsync(archived, fixture.Token);
        await fixture.Repository.SetTaskArchivedAsync(archived.Id, true, fixture.Token);
        await fixture.ExecuteAsync("DROP TABLE agent_task_failures; DROP TABLE conversation_model_preferences; DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=7;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(30, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Task with { IsPinned = true }, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(archived, Assert.Single(await fixture.Repository.GetArchivedTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        var project = Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token));
        Assert.Equal(fixture.Project with { AuthorizedDirectories = project.AuthorizedDirectories }, project);
        Assert.Equal(fixture.Project.AuthorizedDirectories, project.AuthorizedDirectories);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM agent_task_failures;"));
    }

    [Fact]
    public async Task MessageFailure_RollsBackStatusAndDiagnosticTogether()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var failed = fixture.Task with { FailureReason = TaskFailureReason.StreamLineLimit };
        var message = new ConversationMessage(Guid.NewGuid(), failed.Id, MessageRole.Assistant, "partial reply", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(failed, [message], fixture.Token);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskWithMessagesAsync(
            failed with { Status = AgentTaskStatus.Completed, FailureReason = null }, [message], fixture.Token));
        Assert.Equal(failed, Assert.Single(await fixture.Repository.GetTasksAsync(failed.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(failed.Id, fixture.Token)));
        Assert.Equal(1, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }

    [Fact]
    public async Task ForeignMessages_CannotChangeDiagnosticOrCreateTask()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var foreign = fixture.Task with { Id = Guid.NewGuid(), FailureReason = TaskFailureReason.StreamEventLimit };
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.Assistant, "foreign reply", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.SaveTaskWithMessagesAsync(foreign, [message], fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM agent_task_failures;"));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
    }
}
