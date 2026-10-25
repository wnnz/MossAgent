using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;
using static MossAgent.Infrastructure.Tests.ConversationDraftAttachmentTestData;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationSteeringRecoveryTests
{
    [Fact]
    public async Task RestartInterruptsPendingAttachmentInputWithoutReplayingIt()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture.Inner, "interrupted snapshot bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [attachment])], fixture.Token));
        var run = await fixture.OpenAsync();
        var inputId = Guid.NewGuid();
        var ack = Assert.IsType<ConversationSteeringAcceptance>(await repository.AcceptAsync(run.TaskId, run.Id, inputId,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [attachment]), fixture.Token));
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.False((await fixture.ReloadAsync(run)).IsOpen);
        Assert.Equal(1, await fixture.CountAsync(
            $"SELECT COUNT(*) FROM conversation_steering_input_attachments WHERE input_id='{inputId:D}';"));
        var recovered = Assert.Single((await fixture.InputsAsync()).Items);
        Assert.Equal(ConversationSteeringInputState.Interrupted, recovered.State);
        Assert.NotNull(recovered.InterruptedAt);
        Assert.Null(recovered.AppliedAt);
        Assert.Equal(ack.Input.AttachmentItems, recovered.AttachmentItems);
        var restored = Assert.Single(recovered.AttachmentItems);
        Assert.Equal(attachment.Id, restored.Id);
        Assert.Equal(inputId, restored.MessageId);
        Assert.Equal(run.TaskId, restored.TaskId);
        Assert.Equal(64, restored.Sha256.Length);
        var pending = Assert.IsType<ConversationSteeringPending>(
            await repository.GetPendingAsync(run.TaskId, run.Id, fixture.Token));
        Assert.Empty(pending.Inputs);
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
        Assert.Null(await repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), "late", null, fixture.Token));
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(ConversationSteeringInputState.Interrupted,
            Assert.Single((await fixture.InputsAsync()).Items).State);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_steering_input_attachments;"));
    }

    [Fact]
    public async Task UnappliedAttachmentInputReadsIdenticallyAcrossRepositoryInstances()
    {
        await using var fixture = await ConversationSteeringTestFixture.CreateAsync();
        var repository = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var drafts = Repository(fixture.Inner);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var first = Attachment(fixture.Inner, "first snapshot bytes");
        var second = Attachment(fixture.Inner, "second snapshot bytes");
        Assert.True(await drafts.SaveAsync([new(scope, "guidance", 0, [first, second])], fixture.Token));
        var run = await fixture.OpenAsync();
        var inputId = Guid.NewGuid();
        var ack = Assert.IsType<ConversationSteeringAcceptance>(await repository.AcceptAsync(run.TaskId, run.Id, inputId,
            "guidance", new ConversationDraftUpdate(scope, string.Empty, 1, [first, second]), fixture.Token));
        Assert.Equal(new[] { first.Id, second.Id }, ack.Input.AttachmentItems.Select(item => item.Id));
        var reopened = new SqliteConversationSteeringRepository(fixture.Connections,
            paths: new AppDataPaths(fixture.Project.PrimaryDirectory));
        var pending = Assert.IsType<ConversationSteeringPending>(
            await reopened.GetPendingAsync(run.TaskId, run.Id, fixture.Token));
        var pendingInput = Assert.Single(pending.Inputs);
        Assert.Equal(ConversationSteeringInputState.Pending, pendingInput.State);
        Assert.Equal(ack.Input.AttachmentItems, pendingInput.AttachmentItems);
        var history = Assert.Single((await reopened.GetInputsAsync(run.TaskId, long.MaxValue, 64, fixture.Token)).Items);
        Assert.Equal(pendingInput.AttachmentItems, history.AttachmentItems);
        var sources = new[] { first, second };
        for (var index = 0; index < sources.Length; index++)
        {
            Assert.Equal(sources[index].Id, history.AttachmentItems[index].Id);
            Assert.Equal(sources[index].DisplayPath, history.AttachmentItems[index].DisplayPath);
            Assert.Equal(sources[index].ByteCount, history.AttachmentItems[index].ByteCount);
            Assert.Equal("text/plain", history.AttachmentItems[index].MediaType);
            Assert.Equal(inputId, history.AttachmentItems[index].MessageId);
            Assert.Equal(run.TaskId, history.AttachmentItems[index].TaskId);
        }
        Assert.Empty(await fixture.Workspaces.GetMessagesAsync(run.TaskId, fixture.Token));
    }

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
