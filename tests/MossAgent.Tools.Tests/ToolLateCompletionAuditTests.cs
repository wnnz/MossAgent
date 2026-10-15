using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolLateCompletionAuditTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCompletion_PersistsInterruptedAuditAndKeepsAnAlreadyCreatedArtifact(bool cancelCaller)
    {
        var token = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tool = new LateCompletionProbeTool();
        Task<ToolResult>? execution = null;
        try
        {
            var (connections, task) = await InitializeAsync(root, token);
            var clock = new ManualToolTimeProvider();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var executor = new ToolExecutor(new ToolRegistry([tool]),
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)),
                new SqliteToolExecutionAuditSink(connections), clock);
            var context = new ToolExecutionContext(task.Id, root, [root], ApprovalPolicy.FullAccess,
                Timeout: TimeSpan.FromMinutes(1), MaximumOutputCharacters: 32);
            execution = executor.ExecuteAsync(new("late-audit", tool.Descriptor.Name, JsonSerializer.SerializeToElement(new { })),
                context, cancellation.Token);
            await tool.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            var artifactPath = Path.Combine(root, "snapshot.txt");
            var bytes = Encoding.UTF8.GetBytes("snapshot");
            await File.WriteAllBytesAsync(artifactPath, bytes, token);
            var artifact = new ToolArtifact("snapshot", artifactPath, "text/plain", bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)));
            clock.Advance(TimeSpan.FromMinutes(2), false);
            if (cancelCaller) cancellation.Cancel();
            Assert.False(execution.IsCompleted);
            tool.Completion.TrySetResult(ToolResult.Success("late", dialogue: [new("assistant", "retained")]) with { Artifacts = [artifact] });

            var result = await execution.WaitAsync(TimeSpan.FromSeconds(5), token);

            Assert.False(result.IsSuccess);
            Assert.Equal(cancelCaller ? "tool_cancelled" : "tool_timeout", result.ErrorCode);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(artifactPath, token));
            await AssertStoredAsync(connections, result, artifact, token);
        }
        finally
        {
            tool.Completion.TrySetResult(ToolResult.Success("cleanup"));
            if (execution is not null) await execution.WaitAsync(TimeSpan.FromSeconds(5), token);
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(SqliteConnectionFactory Connections, AgentTask Task)> InitializeAsync(string root, CancellationToken token)
    {
        var paths = new AppDataPaths(root);
        var connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(token);
        var repository = new SqliteWorkspaceRepository(connections);
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", root, [root], DateTimeOffset.UtcNow);
        await repository.SaveProjectAsync(project, token);
        var task = new AgentTask(Guid.NewGuid(), project.Id, "fixture", ApprovalPolicy.FullAccess,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await repository.SaveTaskAsync(task, token);
        return (connections, task);
    }

    private static async Task AssertStoredAsync(SqliteConnectionFactory connections, ToolResult result, ToolArtifact artifact, CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT execution.is_success,execution.error_code,execution.output_length,
                artifact.path,artifact.length,artifact.sha256
            FROM tool_executions execution
            JOIN tool_artifacts artifact ON artifact.execution_id=execution.id
            WHERE execution.call_id='late-audit';
            """;
        await using var reader = await command.ExecuteReaderAsync(token);
        Assert.True(await reader.ReadAsync(token));
        Assert.False(reader.GetBoolean(0));
        Assert.Equal(result.ErrorCode, reader.GetString(1));
        Assert.Equal(result.OutputCharacterCount, reader.GetInt32(2));
        Assert.Equal(artifact.Path, reader.GetString(3));
        Assert.Equal(artifact.Length, reader.GetInt64(4));
        Assert.Equal(artifact.Sha256, reader.GetString(5));
        Assert.False(await reader.ReadAsync(token));
    }
}
