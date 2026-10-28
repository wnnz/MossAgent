using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Artifacts;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ArtifactHandlePreviewFlowTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("read-refused")]
    [InlineData("write-refused")]
    [InlineData("cancelled")]
    public async Task SavedModelEditThenPreviewUsesNativeWriteReadAndPersistedArtifactWithoutReplay(string scenario)
    {
        var reads = new TrackedArtifactReadValidator();
        var writes = new TrackedPreviewWriteSessions();
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n", configure: services =>
        {
            Assert.Equal(typeof(WindowsFileReadHandleValidator), Assert.Single(services,
                item => item.ServiceType == typeof(IFileReadHandleValidator)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IFileReadHandleValidator>(reads));
            services.Replace(ServiceDescriptor.Singleton<IAtomicFileWriteSessionFactory>(writes));
        });
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        reads.ArtifactRoot = fixture.Services.GetRequiredService<ITaskArtifactPaths>().GetTaskDirectory(fixture.TaskId);
        reads.Refuse = scenario == "read-refused";
        writes.Refuse = scenario == "write-refused";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        if (scenario == "cancelled") reads.BeforeArtifactRead = cancellation.Cancel;
        var pane = fixture.Workspace.FilePreview;
        fixture.Workspace.ComposerText = "保留前台草稿";
        Assert.True(pane.HasTask);
        await pane.OpenAsync(fixture.FilePath, cancellation.Token);
        Assert.Equal("保留前台草稿", fixture.Workspace.ComposerText);
        Assert.Equal(AgentTaskStatus.Completed, fixture.Workspace.Catalog.SelectedTask!.Status);
        Assert.Equal(scenario == "success", pane.Active!.IsReady);
        Assert.Equal(scenario == "success" ? "beta\n" : "", pane.Active.Text);
        Assert.Equal(1, writes.PreviewCreates);
        Assert.True(reads.ArtifactReads == (scenario == "write-refused" ? 0 : 1),
            $"expected {(scenario == "write-refused" ? 0 : 1)} artifact read(s), got {reads.ArtifactReads}:\n"
            + string.Join("\n---\n", reads.Stacks));
        if (scenario != "write-refused") Assert.False(reads.ArtifactStream!.CanRead);
        if (scenario != "success") Assert.Equal(InspectorText.Get(scenario == "cancelled" ? "PreviewCancelled" : "PreviewFailed"), pane.Active.Status);
        Assert.DoesNotContain("synthetic", pane.Active.Status);
        var audits = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 20, fixture.Token);
        var preview = Assert.Single(audits, audit => audit.ToolName == "filesystem.preview_file");
        Assert.Equal(scenario != "write-refused", preview.IsSuccess);
        Assert.Equal(scenario == "write-refused" ? "artifact_write_unsafe" : null, preview.ErrorCode);
        var artifacts = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>().GetArtifactsAsync(preview.Id, fixture.Token);
        Assert.Equal(scenario == "write-refused" ? 0 : 1, artifacts.Count);
        if (scenario != "write-refused") await AssertReopenedAsync(fixture, Assert.Single(artifacts));
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
    }

    private static async Task AssertReopenedAsync(WorkspaceFileEditingFixture fixture, ToolArtifactRecord artifact)
    {
        await using var services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = services.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        Assert.Empty(workspace.FilePreview.Tabs);
        await workspace.FilePreview.OpenArtifactAsync(artifact, fixture.Token);
        Assert.True(workspace.FilePreview.Active!.IsReady);
        Assert.Equal("beta\n", workspace.FilePreview.Active.Text);
        Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask!.Status);
    }
}
