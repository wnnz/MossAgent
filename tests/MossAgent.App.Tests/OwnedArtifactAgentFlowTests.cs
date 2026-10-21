using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Artifacts;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class OwnedArtifactAgentFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedModelApprovalEditReviewUndoAndReloadUseNativeOwnedArtifacts(bool replace)
    {
        var sessions = new ObservedArtifactSessions();
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, "beta\n", configure: services =>
        {
            Assert.Equal(typeof(WindowsArtifactFileSessionFactory), Assert.Single(services,
                service => service.ServiceType == typeof(IArtifactFileSessionFactory)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IArtifactFileSessionFactory>(sessions));
        });
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Empty(sessions.Sessions);
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        Assert.Equal(2, sessions.Sessions.Count);
        Assert.All(sessions.Sessions, session => { Assert.True(session.IsRetained); Assert.False(session.Stream.CanRead); });
        Assert.All(fixture.Server.Requests, request => Assert.Contains("file-flow-model", request));
        fixture.Workspace.ComposerText = "保留前台草稿";
        await fixture.Workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        Assert.True(fixture.Workspace.FilePreview.Active!.IsReady, fixture.Workspace.FilePreview.Active.Status);
        Assert.Contains("-alpha", fixture.Workspace.FilePreview.Active.Text);
        Assert.Contains("+beta", fixture.Workspace.FilePreview.Active.Text);
        Assert.Equal(3, sessions.Sessions.Count);
        Assert.EndsWith(".diff", sessions.Sessions[2].Path);
        Assert.True(sessions.Sessions[2].IsRetained);
        await AssertReloadedReviewAsync(fixture);
        fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        var undo = fixture.Workspace.FileChanges.Undo;
        undo.RequestCommand.Execute(null);
        await undo.ConfirmCommand.ExecuteAsync(null).WaitAsync(fixture.Token);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Empty(fixture.Workspace.FileChanges.Files);
        Assert.Equal("保留前台草稿", fixture.Workspace.ComposerText);
        Assert.Equal(3, fixture.Server.Requests.Count);
        await using var restored = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = restored.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        Assert.Empty(workspace.FileChanges.Files);
        Assert.False(workspace.IsRunning);
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
    }

    private static async Task AssertReloadedReviewAsync(WorkspaceFileEditingFixture fixture)
    {
        await using var restored = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = restored.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        await workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        Assert.True(workspace.FilePreview.Active!.IsReady, workspace.FilePreview.Active.Status);
        Assert.Contains("-alpha", workspace.FilePreview.Active.Text);
        Assert.Contains("+beta", workspace.FilePreview.Active.Text);
        var directory = restored.GetRequiredService<ITaskArtifactPaths>().GetTaskDirectory(fixture.TaskId);
        Assert.Equal(2, Directory.GetFiles(directory, "edit-*.snapshot").Length);
        Assert.Equal(2, Directory.GetFiles(directory, "review-*.diff").Length);
    }
}
