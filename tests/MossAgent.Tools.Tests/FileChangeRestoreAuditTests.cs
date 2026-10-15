using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRestoreAuditTests
{
    [Theory]
    [InlineData(false, false, true, null)]
    [InlineData(false, true, true, "file_changed")]
    [InlineData(true, false, false, "approval_denied")]
    [InlineData(true, false, true, null)]
    public async Task MutationsAndRejections_RetainTheirOriginalTaskApprovalAndRisk(bool create, bool external, bool approve, string? error)
    {
        using var fixture = new FileChangeRestoreFixture();
        var paths = new AppDataPaths(System.IO.Path.Combine(fixture.Root, "data"));
        var connections = new SqliteConnectionFactory(paths);
        try
        {
            await new SqliteDatabaseInitializer(paths, connections).InitializeAsync(fixture.Token);
            var repository = new SqliteWorkspaceRepository(connections);
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "restore audit fixture", fixture.Root, [fixture.Root], now);
            await repository.SaveProjectAsync(project, fixture.Token);
            var task = new AgentTask(fixture.Context.TaskId, project.Id, "restore audit fixture", ApprovalPolicy.AskEveryTime,
                AgentTaskStatus.Completed, now, now, null);
            await repository.SaveTaskAsync(task, fixture.Token);
            var change = await fixture.ChangeAsync(create: create);
            if (external) await File.WriteAllTextAsync(fixture.Path, "external sentinel", fixture.Token);

            var result = await fixture.ExecuteAsync(change, remove: create, approve: approve,
                audit: new SqliteToolExecutionAuditSink(connections));

            Assert.Equal(error, result.ErrorCode);
            var audit = Assert.Single(await new SqliteToolExecutionAuditReader(connections).GetRecentAsync(task.Id, 10, fixture.Token));
            Assert.Equal(task.Id, audit.TaskId);
            Assert.Equal(create ? fixture.Remove.Descriptor.Name : fixture.Restore.Descriptor.Name, audit.ToolName);
            Assert.Equal(create ? ToolRiskLevel.HighRisk : ToolRiskLevel.Mutation, audit.RiskLevel);
            Assert.Equal(approve, audit.ApprovalGranted);
            Assert.Equal(error is null, audit.IsSuccess);
            Assert.Equal(error, audit.ErrorCode);
            Assert.Equal(result.OutputCharacterCount, audit.OutputLength);
            Assert.NotNull(audit.CompletedAt);
            var reopened = new SqliteToolExecutionAuditReader(new SqliteConnectionFactory(paths));
            Assert.Equal(audit, Assert.Single(await reopened.GetRecentAsync(task.Id, 10, fixture.Token)));
            if (create && approve) Assert.False(File.Exists(fixture.Path));
            else Assert.Equal(external ? "external sentinel" : error is null ? "original\r\n" : "agent update\r\n",
                await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        }
        finally
        {
            using var connection = connections.Create();
            SqliteConnection.ClearPool(connection);
        }
    }
}
