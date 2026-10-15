using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileEditingAuditTests
{
    [Fact]
    public async Task ConcurrentEditConflict_IsPersistedAsApprovedButUnsuccessful()
    {
        using var fixture = new FileEditingFixture();
        try
        {
            var paths = new AppDataPaths(fixture.Root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(fixture.Token);
            var repository = new SqliteWorkspaceRepository(connections);
            var project = new ProjectProfile(Guid.NewGuid(), "file audit fixture", fixture.Root,
                [fixture.Root], DateTimeOffset.UtcNow);
            await repository.SaveProjectAsync(project, fixture.Token);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "conflict audit fixture", ApprovalPolicy.AskEveryTime,
                AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            await repository.SaveTaskAsync(task, fixture.Token);
            var context = new ToolExecutionContext(task.Id, fixture.Root, [fixture.Root], task.ApprovalPolicy);
            var executor = new ToolExecutor(new ToolRegistry([fixture.Read, fixture.Write]),
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)),
                new SqliteToolExecutionAuditSink(connections));
            await fixture.SeedAsync("original");
            var read = await executor.ExecuteAsync(FileEditingFixture.Request(fixture.Read,
                new { path = fixture.Path }), context, fixture.Token);
            Assert.True(read.IsSuccess, read.Summary);
            await fixture.SeedAsync("external change");
            var request = FileEditingFixture.Request(fixture.Write, new { path = fixture.Path, content = "agent change" });

            var result = await executor.ExecuteAsync(request, context, fixture.Token);

            Assert.Equal("file_changed", result.ErrorCode);
            Assert.Equal("external change", await fixture.ContentsAsync());
            await using var connection = connections.Create();
            await connection.OpenAsync(fixture.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT task_id,approval_granted,is_success,error_code,completed_at
                FROM tool_executions WHERE call_id=$call;
                """;
            command.Parameters.AddWithValue("$call", request.CallId);
            await using var reader = await command.ExecuteReaderAsync(fixture.Token);
            Assert.True(await reader.ReadAsync(fixture.Token));
            Assert.Equal(task.Id.ToString(), reader.GetString(0));
            Assert.True(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.Equal("file_changed", reader.GetString(3));
            Assert.False(reader.IsDBNull(4));
            fixture.AssertNoTemporaryFiles();
        }
        finally { SqliteConnection.ClearAllPools(); }
    }
}
