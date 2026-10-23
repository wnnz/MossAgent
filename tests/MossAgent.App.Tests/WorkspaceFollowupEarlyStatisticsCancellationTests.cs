using System.Text.Json;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;
using Xunit.Sdk;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceFollowupEarlyStatisticsCancellationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task SupersededStatisticsCancelledBeforeApprovalKeepAnUnknownApprovalAndDoNotReplayTheWrite() => _session.DispatchAsync(async () =>
    {
        EarlyStatisticsCancellationAuditSink? gate = null;
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true,
            configure: services => services.AddSingleton<IToolExecutionAuditSink>(provider => gate = new(
                new SqliteToolExecutionAuditSink(provider.GetRequiredService<SqliteConnectionFactory>()))));
        var workspace = fixture.Workspace;
        var path = Path.Combine(workspace.Catalog.SelectedProject!.PrimaryDirectory, "early-statistics-cancel.txt");
        fixture.Server.ResponseOverride = (request, number) =>
        {
            if (number == 3) gate!.Started.Task.WaitAsync(fixture.Token).GetAwaiter().GetResult();
            return number == 2 ? WriteCall(request, path) : null;
        };
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.AskEveryTime);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        foreach (var prompt in new[] { "write once", "replace the statistics request before approval" })
        {
            workspace.ComposerText = prompt;
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
        }
        workspace.ComposerText = "keep foreground draft";
        fixture.Server.ReleaseResponses();
        var approval = fixture.Main.Approval;
        await fixture.WaitUntilAsync(() => approval.IsPending && approval.ToolName == "filesystem.write_file"
            && approval.CurrentRequestId is { } id && approval.ApproveCommand.CanExecute(id));
        approval.ApproveCommand.Execute(approval.CurrentRequestId);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 4 && !workspace.IsRunning && !workspace.Queue.IsBusy);
        var write = await WorkspaceFollowupAuditAssertions.ReadWriteAsync(fixture, taskId, true);
        var audits = await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token);
        Assert.Equal(3, audits.Count);
        AssertUnapprovedSuccessIsRejected(audits, taskId);
        var superseded = Assert.Single(audits, audit => audit.ErrorCode == "tool_cancelled");
        Assert.Null(superseded.ApprovalGranted);
        Assert.False(superseded.IsSuccess);
        Assert.NotNull(superseded.CompletedAt);
        Assert.Null(superseded.InterruptedAt);
        Assert.True(write.ApprovalGranted);
        Assert.True(write.IsSuccess);
        Assert.Equal("once", await File.ReadAllTextAsync(path, fixture.Token));
        Assert.Equal("keep foreground draft", workspace.ComposerText);
        Assert.Empty(workspace.Queue.Items);
        Assert.False(approval.IsPending);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
        await workspace.ShutdownAsync();
    }, TestContext.Current.CancellationToken);

    private static void AssertUnapprovedSuccessIsRejected(IReadOnlyList<ToolExecutionAuditRecord> audits, Guid taskId)
    {
        var current = Assert.Single(audits, audit => audit.ToolName == "filesystem.change_statistics" && audit.IsSuccess == true);
        foreach (bool? approved in new bool?[] { null, false })
            Assert.Throws<TrueException>(() => WorkspaceFollowupAuditAssertions.Validate(
                audits.Select(audit => audit.Id == current.Id ? audit with { ApprovalGranted = approved } : audit).ToArray(), taskId, true));
    }

    private static string WriteCall(JsonElement request, string path)
    {
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(value => value?.StartsWith("filesystem_write_file__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new { type = "response.output_item.done", item = new
        {
            type = "function_call", call_id = "early-statistics-write", name,
            arguments = JsonSerializer.Serialize(new { path, content = "once" })
        } });
    }
}
