using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ReadHandleAuditTests
{
    [Fact]
    public async Task ApprovedRead_HandleRefusalIsPersistedAsUnsuccessfulWithoutContentOrArtifacts()
    {
        using var fixture = new FileEditingFixture();
        try
        {
            await fixture.SeedAsync("private content must stay unread");
            var paths = new AppDataPaths(fixture.Root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(fixture.Token);
            var repository = new SqliteWorkspaceRepository(connections);
            var project = new ProjectProfile(Guid.NewGuid(), "handle audit", fixture.Root,
                [fixture.Root], DateTimeOffset.UtcNow);
            await repository.SaveProjectAsync(project, fixture.Token);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "handle audit", ApprovalPolicy.FullAccess,
                AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            await repository.SaveTaskAsync(task, fixture.Token);
            var context = fixture.Context with { TaskId = task.Id };
            var guard = new ReadHandleProbe { Check = (stream, _) =>
            {
                Assert.Equal(0, stream.Position);
                throw new UnauthorizedAccessException("read scope refused");
            } };
            var executor = new ToolExecutor(new([fixture.Read]),
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)),
                new SqliteToolExecutionAuditSink(connections), readHandleValidator: guard);
            var request = FileEditingFixture.Request(fixture.Read, new { path = fixture.Path });
            var result = await executor.ExecuteAsync(request, context, fixture.Token);
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Null(result.Content);
            Assert.True(result.Artifacts is null or { Count: 0 });
            var record = Assert.Single(await new SqliteToolExecutionAuditReader(connections)
                .GetRecentAsync(task.Id, 10, fixture.Token));
            Assert.Equal(request.CallId, record.CallId);
            Assert.True(record.ApprovalGranted);
            Assert.False(record.IsSuccess);
            Assert.Equal("path_denied", record.ErrorCode);
            Assert.NotNull(record.CompletedAt);
            Assert.False(guard.Stream!.CanRead);
        }
        finally { SqliteConnection.ClearAllPools(); }
    }
}
