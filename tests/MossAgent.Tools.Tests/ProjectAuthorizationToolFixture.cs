using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class ProjectAuthorizationToolFixture : IAsyncDisposable
{
    private readonly FileEditingFixture _files = new();
    public ProjectAuthorizationChanges Changes { get; } = new();
    public SqliteWorkspaceRepository Repository { get; private set; } = null!;
    public ProjectAuthorizationMonitor Monitor { get; private set; } = null!;
    public ProjectProfile Project { get; private set; } = null!;
    public ToolExecutionContext Context { get; private set; } = null!;
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public SqliteConnectionFactory Connections { get; private set; } = null!;

    public static async Task<ProjectAuthorizationToolFixture> CreateAsync()
    {
        var fixture = new ProjectAuthorizationToolFixture();
        try
        {
            var paths = new AppDataPaths(fixture._files.Root);
            fixture.Connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, fixture.Connections).InitializeAsync(fixture.Token);
            fixture.Repository = new(fixture.Connections, authorizationChanges: fixture.Changes);
            fixture.Project = new(Guid.NewGuid(), "revocation", fixture._files.Root, [fixture._files.Root], DateTimeOffset.UtcNow);
            await fixture.Repository.SaveProjectAsync(fixture.Project, fixture.Token);
            var task = new AgentTask(Guid.NewGuid(), fixture.Project.Id, "revocation", ApprovalPolicy.AskEveryTime,
                AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            await fixture.Repository.SaveTaskAsync(task, fixture.Token);
            fixture.Monitor = new(fixture.Repository, fixture.Changes);
            fixture.Context = fixture._files.Context with
            {
                TaskId = task.Id, ApprovalPolicy = ApprovalPolicy.AskEveryTime,
                WorkspaceGuard = new ProjectWorkspaceGuard(fixture.Monitor, fixture.Project, task.Id, fixture._files.Root, [fixture._files.Root])
            };
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public ToolExecutor Executor(IAgentTool tool, IToolApprovalService approval) => new(new([tool]), approval,
        new SqliteToolExecutionAuditSink(Connections));

    public Task RevokeAsync() => Repository.SaveProjectAsync(Project with { AuthorizedDirectories = [] }, Token);

    public async Task AssertAuditAsync(string callId, bool? approved)
    {
        var audit = Assert.Single(await new SqliteToolExecutionAuditReader(Connections).GetRecentAsync(Context.TaskId, 10, Token));
        Assert.Equal(callId, audit.CallId);
        Assert.Equal("workspace_authorization_changed", audit.ErrorCode);
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.False(audit.IsSuccess);
        Assert.NotNull(audit.CompletedAt);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (Monitor is not null) await Monitor.DisposeAsync(); }
        finally
        {
            using var connection = Connections?.Create();
            if (connection is not null) Microsoft.Data.Sqlite.SqliteConnection.ClearPool(connection);
            _files.Dispose();
        }
    }
}
