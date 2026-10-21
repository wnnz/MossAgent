using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class RootObjectAuthorizationStatisticsTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task UnchangedPathsReauthorizeStatisticsReviewAndUndoWithoutModelReplay() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n");
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        var workspace = fixture.Workspace;
        var statistics = workspace.FileChanges.Statistics;
        while (!statistics.HasTotals && !statistics.HasError) await Task.Delay(15, fixture.Token);
        Assert.True(statistics.HasTotals);
        var reader = fixture.Services.GetRequiredService<IToolExecutionAuditReader>();
        var before = (await reader.GetRecentAsync(fixture.TaskId, 30, fixture.Token)).Count(audit => audit.ToolName == "filesystem.change_statistics");
        workspace.ComposerText = "reauthorization draft";
        var editor = workspace.Catalog.ProjectEditor;
        editor.Edit(workspace.Catalog.SelectedProject);
        await editor.ConfirmRootObjectsCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        IReadOnlyList<ToolExecutionAuditRecord> audits;
        do
        {
            await Task.Delay(15, deadline.Token);
            audits = await reader.GetRecentAsync(fixture.TaskId, 30, deadline.Token);
        } while (!statistics.HasTotals || audits.Count(audit => audit.ToolName == "filesystem.change_statistics" && audit.CompletedAt is not null) <= before);
        Assert.Contains(audits, audit => audit.ToolName == "filesystem.change_statistics" && audit.IsSuccess == true);
        await workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        Assert.True(workspace.FilePreview.Active!.IsReady, workspace.FilePreview.Active.Status);
        workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        workspace.FileChanges.Undo.RequestCommand.Execute(null);
        await workspace.FileChanges.Undo.ConfirmCommand.ExecuteAsync(null);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("reauthorization draft", workspace.ComposerText);
        Assert.Equal(3, fixture.Server.Requests.Count);
    }, TestContext.Current.CancellationToken);
}
