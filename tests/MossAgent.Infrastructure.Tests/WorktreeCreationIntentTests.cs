using Microsoft.Data.Sqlite;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeCreationIntentTests : IAsyncLifetime
{
    private GitWorktreeSecurityFixture? _fixture;
    private SqliteFailurePersistenceFixture? _storage;
    private GitWorktreeSecurityFixture Fixture => _fixture!;
    private SqliteFailurePersistenceFixture Storage => _storage!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await GitWorktreeSecurityFixture.CreateAsync();
        try
        {
            _storage = await SqliteFailurePersistenceFixture.CreateAsync();
        }
        catch { _fixture.Dispose(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_storage is not null) await _storage.DisposeAsync();
        _fixture?.Dispose();
    }

    [Fact]
    public async Task CreateRecordsIntentAndFirstSaveClaimsAtomically()
    {
        var repository = Storage.Repository;
        var project = await SeedProjectAsync();
        var claims = new StubWorktreeClaimFactory();
        var service = new GitWorktreeService(Fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()),
            roots: new RootIdentityProbe(), connections: Storage.Connections, targets: claims);
        var taskId = Guid.NewGuid();

        var target = await service.CreateAsync(project, taskId, Fixture.Token);
        var head = (await Fixture.GitAsync(Fixture.Repository, "rev-parse", "HEAD")).Trim();

        var intent = await ReadIntentAsync(taskId);
        Assert.Equal(0, intent.State);
        Assert.Equal(project.Id, intent.ProjectId);
        Assert.Equal(Path.GetFullPath(Fixture.Project.PrimaryDirectory),
            Path.GetFullPath(intent.Repository), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(head, intent.BaseCommit);
        Assert.Equal(target, intent.Target);
        Assert.False(string.IsNullOrEmpty(intent.VolumeId));
        Assert.False(string.IsNullOrEmpty(intent.FileId));
        Assert.Null(intent.ClaimedAt);

        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, project.Id, "claimed task", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, target);
        var message = new ConversationMessage(Guid.NewGuid(), taskId, MessageRole.User, "prompt", now);
        await repository.SaveTaskWithMessagesAsync(task, [message], Fixture.Token);

        intent = await ReadIntentAsync(taskId);
        Assert.Equal(1, intent.State);
        Assert.NotNull(intent.ClaimedAt);
        Assert.Single(await repository.GetTasksAsync(project.Id, Fixture.Token));
        Assert.Single(await repository.GetMessagesAsync(taskId, Fixture.Token));
    }

    [Fact]
    public async Task FirstSaveWithoutIntentIsRefusedAndPersistsNothing()
    {
        var repository = Storage.Repository;
        var project = await SeedProjectAsync();
        var taskId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, project.Id, "orphan", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, Path.Combine(Fixture.Paths.WorktreesPath, taskId.ToString("N")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveTaskWithMessagesAsync(task,
                [new ConversationMessage(Guid.NewGuid(), taskId, MessageRole.User, "prompt", now)], Fixture.Token));

        Assert.Empty(await repository.GetTasksAsync(project.Id, Fixture.Token));
        Assert.Empty(await repository.GetMessagesAsync(taskId, Fixture.Token));
    }

    [Fact]
    public async Task IntentForDifferentTargetCannotClaimTask()
    {
        var repository = Storage.Repository;
        var project = await SeedProjectAsync();
        var intents = new SqliteWorktreeCreationIntents(Storage.Connections);
        var taskId = Guid.NewGuid();
        await intents.RecordAsync(taskId, project, project.PrimaryDirectory, null, "commit",
            Path.Combine(Fixture.Paths.WorktreesPath, taskId.ToString("N")), Fixture.Token);

        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, project.Id, "mismatched", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, Fixture.Outside);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.SaveTaskAsync(task, Fixture.Token));
        Assert.Equal(0, (await ReadIntentAsync(taskId)).State);
        Assert.Empty(await repository.GetTasksAsync(project.Id, Fixture.Token));
    }

    [Fact]
    public async Task ClaimedTaskContinuesSavingAfterTheClaim()
    {
        var repository = Storage.Repository;
        var project = await SeedProjectAsync();
        var intents = new SqliteWorktreeCreationIntents(Storage.Connections);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(Fixture.Paths.WorktreesPath, taskId.ToString("N"));
        await intents.RecordAsync(taskId, project, project.PrimaryDirectory, null, "commit", target, Fixture.Token);

        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, project.Id, "task", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, target);
        await repository.SaveTaskAsync(task, Fixture.Token);
        await repository.SaveTaskAsync(task with { Status = AgentTaskStatus.Completed }, Fixture.Token);

        Assert.Equal(AgentTaskStatus.Completed,
            Assert.Single(await repository.GetTasksAsync(project.Id, Fixture.Token)).Status);
    }

    [Fact]
    public async Task RecordingIntentForPersistedTaskIsDenied()
    {
        var repository = Storage.Repository;
        var project = await SeedProjectAsync();
        var intents = new SqliteWorktreeCreationIntents(Storage.Connections);
        var taskId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(taskId, project.Id, "plain task", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, null);
        await repository.SaveTaskAsync(task, Fixture.Token);

        await Assert.ThrowsAsync<WorktreeAuthorizationException>(() => intents.RecordAsync(
            taskId, project, project.PrimaryDirectory, null, "commit",
            Path.Combine(Fixture.Paths.WorktreesPath, taskId.ToString("N")), Fixture.Token));
    }

    private async Task<ProjectProfile> SeedProjectAsync()
    {
        await Storage.Repository.SaveProjectAsync(Fixture.Project, Fixture.Token);
        var stored = await Storage.Repository.GetProjectsAsync(Fixture.Token);
        return Assert.Single(stored, item => item.Id == Fixture.Project.Id);
    }

    private async Task<WorktreeCreationIntentRow> ReadIntentAsync(Guid taskId)
    {
        await using var connection = Storage.Connections.Create();
        await connection.OpenAsync(Fixture.Token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT project_id,repository,base_commit,target,volume_id,file_id,state,claimed_at
            FROM worktree_creation_intents WHERE task_id=$task;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(Fixture.Token);
        Assert.True(await reader.ReadAsync(Fixture.Token));
        return new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));
    }
}
