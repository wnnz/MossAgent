using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ToolExecutorAuditTests
{
    [Fact]
    public async Task ExecuteAsync_PersistsCompletedAudit()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var setup = await CreateSetupAsync(root, cancellationToken);
            var tool = new DirectoryListTool(new AuthorizedPathResolver());
            var registry = new ToolRegistry([tool]);
            var executor = new ToolExecutor(
                registry,
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(false)),
                new SqliteToolExecutionAuditSink(setup.Connections));
            var request = new ToolRequest(
                "call-audit", tool.Descriptor.Name,
                System.Text.Json.JsonDocument.Parse("{\"path\":\".\"}").RootElement.Clone());
            var context = new ToolExecutionContext(
                setup.Task.Id, root, [root], ApprovalPolicy.ReadOnly);

            var result = await executor.ExecuteAsync(request, context, cancellationToken);

            Assert.True(result.IsSuccess, result.Summary);
            await AssertAuditAsync(setup.Connections, cancellationToken);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsWhenAuditCannotStart()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var paths = new AppDataPaths(root);
            var connections = new SqliteConnectionFactory(paths);
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
            var tool = new DirectoryCreateTool(new AuthorizedPathResolver());
            var registry = new ToolRegistry([tool]);
            var executor = new ToolExecutor(
                registry,
                new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)),
                new SqliteToolExecutionAuditSink(connections));
            var target = Path.Combine(root, "must-not-exist");
            var request = new ToolRequest(
                "call-no-task", tool.Descriptor.Name,
                System.Text.Json.JsonSerializer.SerializeToElement(new { path = target }));
            var context = new ToolExecutionContext(
                Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

            var result = await executor.ExecuteAsync(request, context, cancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("audit_unavailable", result.ErrorCode);
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidArgumentsBeforeApproval()
    {
        var approvalRequests = 0;
        var tool = new DirectoryCreateTool(new AuthorizedPathResolver());
        var executor = new ToolExecutor(
            new ToolRegistry([tool]),
            new PolicyApprovalService((_, _, _) =>
            {
                approvalRequests++;
                return ValueTask.FromResult(true);
            }));
        var request = new ToolRequest(
            "call-invalid-arguments",
            tool.Descriptor.Name,
            System.Text.Json.JsonSerializer.SerializeToElement(new { }));
        var root = CreateRoot();
        try
        {
            var context = new ToolExecutionContext(
                Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

            var result = await executor.ExecuteAsync(
                request, context, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("invalid_arguments", result.ErrorCode);
            Assert.Contains("path", result.Summary, StringComparison.Ordinal);
            Assert.Equal(0, approvalRequests);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInvalidToolSchemaBeforeExecution()
    {
        var tool = new InvalidSchemaTool();
        var executor = new ToolExecutor(
            new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        var context = new ToolExecutionContext(
            Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);
        var request = new ToolRequest(
            "call-invalid-schema",
            tool.Descriptor.Name,
            System.Text.Json.JsonSerializer.SerializeToElement(new { }));

        var result = await executor.ExecuteAsync(
            request, context, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("tool_schema_invalid", result.ErrorCode);
        Assert.False(tool.WasExecuted);
    }

    private static async Task AssertAuditAsync(
        SqliteConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tool_name,approval_granted,is_success,error_code
            FROM tool_executions WHERE call_id='call-audit';
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("filesystem.list_directory", reader.GetString(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.IsDBNull(3));
    }

    private static async Task<(SqliteConnectionFactory Connections, AgentTask Task)> CreateSetupAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var paths = new AppDataPaths(root);
        var connections = new SqliteConnectionFactory(paths);
        await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(cancellationToken);
        var repository = new SqliteWorkspaceRepository(connections);
        var project = new ProjectProfile(
            Guid.NewGuid(), "test", root, [root], DateTimeOffset.UtcNow);
        await repository.SaveProjectAsync(project, cancellationToken);
        var task = new AgentTask(
            Guid.NewGuid(), project.Id, "test", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await repository.SaveTaskAsync(task, cancellationToken);
        return (connections, task);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
