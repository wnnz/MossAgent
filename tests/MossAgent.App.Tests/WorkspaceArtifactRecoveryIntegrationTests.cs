using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceArtifactRecoveryIntegrationTests
{
    [Fact]
    public async Task OrphanedRecordListsAndAcknowledgeClosesItWithoutTouchingTasksOrFiles()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var project = workspace.Catalog.SelectedProject!;
        var paths = new AppDataPaths(fixture.DataRoot);
        var connections = new SqliteConnectionFactory(paths);
        var journal = new SqliteArtifactSessionJournal(connections, new TaskArtifactPaths(paths));
        var taskId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await fixture.Tasks.SaveTaskAsync(new AgentTask(taskId, project.Id, "artifact task",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Failed, now, now, null), fixture.Token);
        var directory = Path.Combine(paths.ArtifactsPath, taskId.ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "edit-leftover.txt");
        await File.WriteAllTextAsync(file, "leftover", fixture.Token);
        var recordId = await journal.RecordAsync(taskId, "edit-leftover.txt", file,
            ArtifactSessionKind.Artifact, null, fixture.Token);
        await ExecuteAsync(connections,
            "UPDATE artifact_session_records SET updated_at='2000-01-01T00:00:00.0000000+00:00' WHERE stage IN (0,1,4);",
            fixture.Token);
        await journal.ReconcileAsync(fixture.Token);

        await workspace.ArtifactRecovery.RefreshAsync();

        Assert.True(workspace.ArtifactRecovery.IsVisible);
        var row = Assert.Single(workspace.ArtifactRecovery.Items);
        Assert.Equal(recordId, row.RecordId);
        Assert.Equal(taskId, row.TaskId);
        Assert.Contains("暂存残留", row.Summary);
        var draft = workspace.ComposerText;

        await row.AcknowledgeCommand.ExecuteAsync(null);

        Assert.Empty(workspace.ArtifactRecovery.Items);
        Assert.Empty(await journal.ReadUnresolvedAsync(project, fixture.Token));
        Assert.True(File.Exists(file));
        Assert.Equal(draft, workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
    }

    private static async Task ExecuteAsync(SqliteConnectionFactory connections, string commandText,
        CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(token);
    }
}
