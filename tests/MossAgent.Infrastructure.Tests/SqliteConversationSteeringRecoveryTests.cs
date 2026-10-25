using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringRecoveryTests
{
    [Fact]
    public async Task ReapplyingAMissingVersionMarkerPreservesExistingReceiptsAndMessages()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var applied = await fixture.AcceptAsync(run, "already applied");
        Assert.NotNull(await fixture.Repository.ApplyAsync(await fixture.ReloadAsync(run), [applied.Id], null, fixture.Token));
        var pending = await fixture.AcceptAsync(run, "must not replay");
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=25;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=25;"));
        Assert.Equal(applied.Id, Assert.Single(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Id);
        var inputs = (await fixture.InputsAsync()).Items;
        Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, Assert.Single(inputs, item => item.Id == applied.Id).State);
        Assert.Equal(ConversationSteeringInputState.Interrupted, Assert.Single(inputs, item => item.Id == pending.Id).State);
    }

    [Fact]
    public async Task RestartInterruptsOnlyPendingInputAndNeverReplaysIt()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var applied = await fixture.AcceptAsync(run, "applied once");
        run = await fixture.ReloadAsync(run);
        Assert.NotNull(await fixture.Repository.ApplyAsync(run, [applied.Id], null, fixture.Token));
        var pending = await fixture.AcceptAsync(run, "not applied");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var recoveredRun = await fixture.ReloadAsync(run);
        var recovered = (await fixture.InputsAsync()).Items.ToArray();
        Assert.False(recoveredRun.IsOpen);
        Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, Assert.Single(recovered, item => item.Id == applied.Id).State);
        var interrupted = Assert.Single(recovered, item => item.Id == pending.Id);
        Assert.Equal(ConversationSteeringInputState.Interrupted, interrupted.State);
        Assert.NotNull(interrupted.InterruptedAt);
        Assert.Null(interrupted.AppliedAt);
        var task = Assert.Single(await fixture.Workspaces.GetTasksAsync(fixture.Task.ProjectId, fixture.Token));
        Assert.Equal(AgentTaskStatus.Interrupted, task.Status);
        Assert.Equal(applied.Id, Assert.Single(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token)).Id);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(recovered, (await fixture.InputsAsync()).Items);
        Assert.Equal(recoveredRun, await fixture.ReloadAsync(run));
        Assert.Null(await fixture.Repository.OpenAsync(task, Guid.NewGuid(), fixture.Token));
        Assert.Null(await fixture.Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "late", null, fixture.Token));
    }

    [Fact]
    public async Task RecoveryFailureRollsBackTaskRunAndReceiptTogether()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var run = await fixture.OpenAsync();
        var input = await fixture.AcceptAsync(run);
        run = await fixture.ReloadAsync(run);
        await fixture.ExecuteAsync("CREATE TRIGGER reject_input_recovery BEFORE UPDATE ON conversation_steering_inputs BEGIN SELECT RAISE(ABORT,'synthetic recovery failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(run, await fixture.ReloadAsync(run));
        Assert.Equal(input, Assert.Single((await fixture.InputsAsync()).Items));
        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Workspaces.GetTasksAsync(fixture.Task.ProjectId, fixture.Token)).Status);
        await fixture.ExecuteAsync("DROP TRIGGER reject_input_recovery;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.False((await fixture.ReloadAsync(run)).IsOpen);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
    }

    [Fact]
    public async Task ForwardMigrationIsAtomicRetryableAndPreservesExistingData()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "legacy", DateTimeOffset.UtcNow);
        await fixture.Workspaces.AppendMessageAsync(message, fixture.Token);
        await fixture.ExecuteAsync("DROP TABLE conversation_steering_input_attachments; DROP TABLE conversation_steering_inputs; DROP TABLE conversation_steering_runs; DELETE FROM schema_versions WHERE version IN(25,39); CREATE TRIGGER reject_steering_version BEFORE INSERT ON schema_versions WHEN NEW.version=25 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'conversation_steering_%';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=25;"));
        Assert.Equal(message, Assert.Single(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(AgentTaskStatus.Running, Assert.Single(await fixture.Workspaces.GetTasksAsync(fixture.Task.ProjectId, fixture.Token)).Status);
        await fixture.ExecuteAsync("DROP TRIGGER reject_steering_version;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version=25;"));
        Assert.Equal(message, Assert.Single(await fixture.Workspaces.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(24, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions WHERE version<25;"));
        Assert.Empty((await fixture.InputsAsync()).Items);
    }
}
