using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileRevealAuditTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SelectionAndDeniedApprovalAreAuditedWithBoundedOutput(bool approved)
    {
        using var fixture = new FileEditingFixture();
        try
        {
            await fixture.SeedAsync("secret synthetic content not in audit");
            var paths = new AppDataPaths(fixture.Root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(fixture.Token);
            var workspace = new SqliteWorkspaceRepository(connections);
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "synthetic", fixture.Root, [fixture.Root], now);
            await workspace.SaveProjectAsync(project, fixture.Token);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "synthetic", ApprovalPolicy.AskEveryTime,
                AgentTaskStatus.Completed, now, now, null);
            await workspace.SaveTaskAsync(task, fixture.Token);
            var service = new ProbeFileRevealService();
            var tool = new FileRevealTool(fixture.Paths, service);
            var executor = new ToolExecutor(new ToolRegistry([tool]),
                new PolicyApprovalService((_, _, _) => ValueTask.FromResult(approved)),
                new SqliteToolExecutionAuditSink(connections));
            var request = FileEditingFixture.Request(tool, new { path = fixture.Path });
            var result = await executor.ExecuteAsync(request, fixture.Context with
            { TaskId = task.Id, ApprovalPolicy = task.ApprovalPolicy, MaximumOutputCharacters = 4 }, fixture.Token);
            Assert.Equal(approved, result.IsSuccess);
            Assert.True(result.OutputCharacterCount <= 4);
            Assert.Equal(approved ? 1 : 0, service.Paths.Count);
            await using var connection = connections.Create();
            await connection.OpenAsync(fixture.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT approval_granted,is_success,output_length,completed_at FROM tool_executions WHERE call_id=$call;";
            command.Parameters.AddWithValue("$call", request.CallId);
            await using var reader = await command.ExecuteReaderAsync(fixture.Token);
            Assert.True(await reader.ReadAsync(fixture.Token));
            Assert.Equal(approved, reader.GetBoolean(0));
            Assert.Equal(approved, reader.GetBoolean(1));
            Assert.InRange(reader.GetInt32(2), 0, 4);
            Assert.False(reader.IsDBNull(3));
            Assert.Equal("secret synthetic content not in audit", await fixture.ContentsAsync());
        }
        finally { SqliteConnection.ClearAllPools(); }
    }
}
