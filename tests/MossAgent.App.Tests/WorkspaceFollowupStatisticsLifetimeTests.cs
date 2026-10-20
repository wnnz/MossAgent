using System.Text.Json;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;
using Xunit;
using Xunit.Sdk;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceFollowupStatisticsLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SupersededStatisticsAreObservedWithoutHidingCurrentFailures(bool failCurrent) => _session.DispatchAsync(async () =>
    {
        var gate = new StatisticsCompletionGate(failCurrent);
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true,
            configure: services => services.AddSingleton<ITaskProcessLauncher>(gate));
        var workspace = fixture.Workspace;
        IReadOnlyList<ToolExecutionAuditRecord>? replaced = null;
        var path = Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "statistics-lifetime.txt");
        fixture.Server.ResponseOverride = (request, number) =>
        {
            if (number == 3) gate.Started.Task.WaitAsync(fixture.Token).GetAwaiter().GetResult();
            if (number == 4) replaced = ReadCancelledSnapshot(fixture);
            return number == 2 ? WriteCall(request, path) : null;
        };
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.AskEveryTime);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        foreach (var prompt in new[] { "write once", "replace file-card model" })
        {
            workspace.ComposerText = prompt;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        workspace.ComposerText = "preserved foreground draft";
        fixture.Server.ReleaseResponses();
        var approval = fixture.Main.Approval;
        await fixture.WaitUntilAsync(() => approval.IsPending && approval.ToolName == "filesystem.write_file"
            && approval.CurrentRequestId is { } id && approval.ApproveCommand.CanExecute(id));
        approval.ApproveCommand.Execute(approval.CurrentRequestId);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 4 && !workspace.IsRunning && !workspace.Queue.IsBusy);
        var superseded = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ToolExecutionAuditRecord>>(replaced),
            item => item.ToolName == "filesystem.change_statistics");
        Assert.False(superseded.IsSuccess);
        Assert.Equal("tool_cancelled", superseded.ErrorCode);
        if (failCurrent)
        {
            var error = await Assert.ThrowsAsync<EqualException>(() => WorkspaceFollowupAuditAssertions.ReadWriteAsync(fixture, taskId, true));
            Assert.Contains("snapshot_review_failed", error.Message);
            Assert.True(workspace.FileChanges.Statistics.HasError);
            Assert.False(workspace.FileChanges.Statistics.HasTotals);
        }
        else await WorkspaceFollowupAuditAssertions.ReadWriteAsync(fixture, taskId, true);
        var audits = await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token);
        Assert.Equal(3, audits.Count);
        var write = Assert.Single(audits, item => item.ToolName == "filesystem.write_file");
        Assert.True(write.IsSuccess);
        Assert.True(write.ApprovalGranted);
        Assert.Equal("once", await File.ReadAllTextAsync(path, fixture.Token));
        Assert.Equal("preserved foreground draft", workspace.ComposerText);
        Assert.Empty(workspace.Queue.Items);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        if (!failCurrent) AssertInvalidSnapshotsFail(audits, taskId, superseded);
        await workspace.ShutdownAsync();
    }, TestContext.Current.CancellationToken);

    private static void AssertInvalidSnapshotsFail(IReadOnlyList<ToolExecutionAuditRecord> audits, Guid taskId,
        ToolExecutionAuditRecord superseded)
    {
        var error = Assert.Throws<EqualException>(() => WorkspaceFollowupAuditAssertions.Validate(
            audits.Select(item => item.Id == superseded.Id ? item with { ErrorCode = "unexpected_error" } : item).ToArray(), taskId, true));
        Assert.Contains("unexpected_error", error.Message);
        var current = audits.Where(item => item.ToolName == "filesystem.change_statistics").MaxBy(item => item.StartedAt)!;
        Assert.Throws<TrueException>(() => WorkspaceFollowupAuditAssertions.Validate(
            audits.Select(item => item.Id == current.Id ? item with { IsSuccess = false, ErrorCode = "tool_cancelled" } : item).ToArray(), taskId, true));
    }

    private static IReadOnlyList<ToolExecutionAuditRecord> ReadCancelledSnapshot(WorkspaceModelSelectionFixture fixture)
    {
        while (true)
        {
            var audits = fixture.ToolAudits.GetRecentAsync(fixture.Workspace.Catalog.SelectedTask!.Id, 10, fixture.Token).GetAwaiter().GetResult();
            if (audits.Any(item => item.ErrorCode == "tool_cancelled")) return audits;
            Task.Delay(1, fixture.Token).GetAwaiter().GetResult();
        }
    }

    private static string WriteCall(JsonElement request, string path)
    {
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(value => value?.StartsWith("filesystem_write_file__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new { type = "response.output_item.done", item = new
        {
            type = "function_call", call_id = "statistics-write", name,
            arguments = JsonSerializer.Serialize(new { path, content = "once" })
        } });
    }
}
