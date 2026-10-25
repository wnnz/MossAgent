using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal sealed class ConversationSteeringTestFixture : IAsyncDisposable
{
    private readonly SqliteFailurePersistenceFixture _inner;
    private ConversationSteeringTestFixture(SqliteFailurePersistenceFixture inner)
    {
        _inner = inner;
        Task = inner.Task with { Status = AgentTaskStatus.Running, FailureReason = null };
        Repository = new(inner.Connections);
    }

    public CancellationToken Token => _inner.Token;
    internal SqliteFailurePersistenceFixture Inner => _inner;
    public ProjectProfile Project => _inner.Project;
    public AgentTask Task { get; }
    public SqliteConversationSteeringRepository Repository { get; }
    public SqliteWorkspaceRepository Workspaces => _inner.Repository;
    public SqliteConnectionFactory Connections => _inner.Connections;
    public SqliteDatabaseInitializer Initializer => _inner.Initializer;
    public Task ExecuteAsync(string sql) => _inner.ExecuteAsync(sql);
    public Task<long> CountAsync(string sql) => _inner.CountAsync(sql);
    public Task<ConversationSteeringRun> OpenAsync() => OpenAsync(Task);
    public async Task<ConversationSteeringRun> OpenAsync(AgentTask task) =>
        Assert.IsType<ConversationSteeringRun>(await Repository.OpenAsync(task, Guid.NewGuid(), Token));
    public async Task<ConversationSteeringRun> ReloadAsync(ConversationSteeringRun run) =>
        Assert.IsType<ConversationSteeringRun>(await Repository.GetRunAsync(run.TaskId, run.Id, Token));
    public Task<ConversationSteeringPage> InputsAsync(Guid? taskId = null) =>
        Repository.GetInputsAsync(taskId ?? Task.Id, long.MaxValue, ConversationSteeringInput.MaximumPageSize, Token);
    public async Task<ConversationSteeringInput> AcceptAsync(ConversationSteeringRun run, string text = "guidance") =>
        Assert.IsType<ConversationSteeringAcceptance>(await Repository.AcceptAsync(run.TaskId, run.Id, Guid.NewGuid(), text, null, Token)).Input;

    public static async Task<ConversationSteeringTestFixture> CreateAsync()
    {
        var inner = await SqliteFailurePersistenceFixture.CreateAsync();
        try
        {
            var fixture = new ConversationSteeringTestFixture(inner);
            await inner.Repository.SaveTaskAsync(fixture.Task, inner.Token);
            return fixture;
        }
        catch { await inner.DisposeAsync(); throw; }
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
