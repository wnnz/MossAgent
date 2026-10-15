using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSnapshotReviewFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualEdit_ReviewsPersistedSnapshotsAfterExternalChangeAndRestore(bool deleted)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n");
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        if (deleted) File.Delete(fixture.FilePath);
        else await File.WriteAllTextAsync(fixture.FilePath, "external sentinel", fixture.Token);

        await fixture.Workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        AssertPatch(fixture.Workspace, fixture.FilePath);
        Assert.Equal(3, fixture.Server.Requests.Count);
        await fixture.Workspace.FilePreview.RefreshCommand.ExecuteAsync(null);
        AssertPatch(fixture.Workspace, fixture.FilePath);
        await fixture.Workspace.FilePreview.OpenCommand.ExecuteAsync(fixture.FilePath);
        Assert.False(fixture.Workspace.FilePreview.Active!.IsDiff);
        await fixture.Workspace.FileChanges.OpenFileCommand.ExecuteAsync(fixture.FilePath);
        AssertPatch(fixture.Workspace, fixture.FilePath);
        Assert.Equal(2, fixture.Workspace.FilePreview.Tabs.Count);

        await using var restored = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = restored.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        await workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        AssertPatch(workspace, fixture.FilePath);
        Assert.Equal(3, fixture.Server.Requests.Count);
        var executions = await restored.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        Assert.Equal(3, executions.Count(execution => execution.ToolName == "filesystem.review_change" && execution.IsSuccess == true));
        Assert.Equal(deleted, !File.Exists(fixture.FilePath));
        if (!deleted) Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
    }

    private static void AssertPatch(WorkspaceViewModel workspace, string path)
    {
        var tab = Assert.IsType<FilePreviewTabViewModel>(workspace.FilePreview.Active);
        Assert.Equal(path, tab.Path);
        Assert.True(tab.IsReady, tab.Status);
        Assert.True(tab.IsDiff);
        Assert.Contains("此任务快照", tab.Status);
        Assert.Contains("-alpha", tab.Text);
        Assert.Contains("+beta", tab.Text);
        Assert.DoesNotContain("external sentinel", tab.Text);
        Assert.False(workspace.FilePreview.CanReveal);
    }
}
