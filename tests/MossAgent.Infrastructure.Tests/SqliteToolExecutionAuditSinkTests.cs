using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteToolExecutionAuditSinkTests
{
    [Fact]
    public async Task StartAndComplete_PersistAuditAndArtifactMetadata()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
            var task = await CreateTaskAsync(connections, cancellationToken);
            var sink = new SqliteToolExecutionAuditSink(connections);
            var executionId = await sink.StartAsync(
                new ToolExecutionAuditStart(
                    task.Id, "call-1", "browser.screenshot", ToolRiskLevel.ReadOnly,
                    ApprovalPolicy.FullAccess, DateTimeOffset.UtcNow),
                cancellationToken);
            var artifact = new ToolArtifact(
                "capture.png", Path.Combine(root, "capture.png"), "image/png", 42, "ABC123");

            await sink.CompleteAsync(
                executionId,
                new ToolExecutionAuditCompletion(
                    DateTimeOffset.UtcNow, true, true, null, 120),
                [artifact], cancellationToken);

            await AssertStoredAsync(connections, executionId, task.Id, cancellationToken);
            var auditReader = new SqliteToolExecutionAuditReader(connections);
            var executions = await auditReader.GetRecentAsync(task.Id, 10, cancellationToken);
            var stored = Assert.Single(executions);
            Assert.Equal(executionId, stored.Id);
            Assert.Equal(1, stored.ArtifactCount);
            var artifacts = await auditReader.GetArtifactsAsync(executionId, cancellationToken);
            Assert.Equal("capture.png", Assert.Single(artifacts).Name);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<AgentTask> CreateTaskAsync(
        SqliteConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var repository = new SqliteWorkspaceRepository(connections);
        var project = new ProjectProfile(
            Guid.NewGuid(), "test", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
        await repository.SaveProjectAsync(project, cancellationToken);
        var task = new AgentTask(
            Guid.NewGuid(), project.Id, "audit", ApprovalPolicy.FullAccess,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await repository.SaveTaskAsync(task, cancellationToken);
        return task;
    }

    private static async Task AssertStoredAsync(
        SqliteConnectionFactory connections,
        Guid executionId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var execution = connection.CreateCommand();
        execution.CommandText = """
            SELECT tool_name,approval_granted,is_success,output_length
            FROM tool_executions WHERE id=$id;
            """;
        execution.Parameters.AddWithValue("$id", executionId.ToString());
        await using var reader = await execution.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("browser.screenshot", reader.GetString(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.Equal(120, reader.GetInt32(3));
        await reader.DisposeAsync();

        await using var artifacts = connection.CreateCommand();
        artifacts.CommandText = "SELECT name,media_type,length,sha256 FROM tool_artifacts WHERE task_id=$task";
        artifacts.Parameters.AddWithValue("$task", taskId.ToString());
        await using var artifactReader = await artifacts.ExecuteReaderAsync(cancellationToken);
        Assert.True(await artifactReader.ReadAsync(cancellationToken));
        Assert.Equal("capture.png", artifactReader.GetString(0));
        Assert.Equal("image/png", artifactReader.GetString(1));
        Assert.Equal(42, artifactReader.GetInt64(2));
        Assert.Equal("ABC123", artifactReader.GetString(3));
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
