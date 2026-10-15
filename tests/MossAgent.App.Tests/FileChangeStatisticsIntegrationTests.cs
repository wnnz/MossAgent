using System.Text.Json;
using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileChangeStatisticsIntegrationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public async Task ContinuousEdits_CountFirstToLastSnapshotsAndUndoReturnsToNetZero()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await _session.DispatchAsync(async () =>
        {
            var workspace = fixture.Workspace;
            await workspace.LoadAsync();
            workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
            var path = Path.Combine(fixture.Project.PrimaryDirectory, "statistics.cs");
            await File.WriteAllTextAsync(path, "one\nkeep\n", fixture.Token);
            await WriteAsync(fixture, path, "two\nkeep\n");
            await WriteAsync(fixture, path, "last\nkeep\n");
            await FileChangeStatisticsStateTests.WaitAsync(() => workspace.FileChanges.Statistics.HasTotals);
            var row = Assert.Single(workspace.FileChanges.Files);
            Assert.Equal(new ToolFileChangeStatistics(1, 1), row.Statistics);
            Assert.Equal("+1", workspace.FileChanges.Statistics.AddedLabel);
            Assert.Equal("-1", workspace.FileChanges.Statistics.RemovedLabel);
            var audit = await fixture.Audit.GetRecentAsync(fixture.Task.Id, 30, fixture.Token);
            Assert.Contains(audit, item => item.ToolName == "filesystem.change_statistics" && item.IsSuccess == true);
            workspace.FileChanges.Undo.RequestCommand.Execute(null);
            await workspace.FileChanges.Undo.ConfirmCommand.ExecuteAsync(null);
            Assert.Empty(workspace.FileChanges.Files);
            Assert.False(workspace.FileChanges.Statistics.HasTotals);
            Assert.Equal("one\nkeep\n", await File.ReadAllTextAsync(path, fixture.Token));
            await workspace.ShutdownAsync();
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MissingSnapshot_DoesNotLeakPartialTotalsAndCanBeExplicitlyRetried()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await _session.DispatchAsync(async () =>
        {
            var workspace = fixture.Workspace;
            await workspace.LoadAsync();
            workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
            var path = Path.Combine(fixture.Project.PrimaryDirectory, "statistics.cs");
            await File.WriteAllTextAsync(path, "one\n", fixture.Token);
            var change = await WriteAsync(fixture, path, "two\n");
            var context = fixture.Panels.CreateContext(fixture.Task, fixture.Project);
            var snapshotPath = Path.Combine(context.ArtifactDirectory!, change.BeforeSnapshot!.FileName);
            var bytes = await File.ReadAllBytesAsync(snapshotPath, fixture.Token);
            File.Delete(snapshotPath);
            await FileChangeStatisticsStateTests.WaitAsync(() => workspace.FileChanges.Statistics.RetryCommand.CanExecute(null));
            Assert.False(workspace.FileChanges.Statistics.HasTotals);
            Assert.True(Assert.Single(workspace.FileChanges.Files).IsStatisticsUnavailable);
            await File.WriteAllBytesAsync(snapshotPath, bytes, fixture.Token);
            workspace.FileChanges.Statistics.RetryCommand.Execute(null);
            await FileChangeStatisticsStateTests.WaitAsync(() => workspace.FileChanges.Statistics.HasTotals);
            Assert.Equal("+1", workspace.FileChanges.Statistics.AddedLabel);
            Assert.Equal("two\n", await File.ReadAllTextAsync(path, fixture.Token));
            await workspace.ShutdownAsync();
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<ToolFileChange> WriteAsync(WorkspacePersistenceFixture fixture, string path, string content)
    {
        var context = fixture.Panels.CreateContext(fixture.Task, fixture.Project) with { ApprovalPolicy = ApprovalPolicy.FullAccess };
        var result = await fixture.Tools.ExecuteAsync(new(Guid.NewGuid().ToString("N"), "filesystem.write_file",
            JsonSerializer.SerializeToElement(new { path, content })), context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        fixture.Workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("filesystem.write_file", true, result.Summary,
            result.Content, FileChanges: result.FileChanges)));
        return Assert.Single(result.FileChanges!);
    }
}
