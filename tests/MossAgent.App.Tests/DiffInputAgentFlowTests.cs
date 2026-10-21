using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DiffInputAgentFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SavedModelEditStatisticsReviewUndoAndRestorationUseNativeInputs(bool replace) => _session.DispatchAsync(async () =>
    {
        var inputs = new ObservedDiffInputSessions();
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, "beta\n", configure: services =>
        {
            Assert.Equal(typeof(WindowsDiffInputFileSessionFactory), Assert.Single(services,
                service => service.ServiceType == typeof(IDiffInputFileSessionFactory)).ImplementationType);
            services.Replace(ServiceDescriptor.Singleton<IDiffInputFileSessionFactory>(inputs));
        });
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Empty(inputs.Sessions);
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        fixture.Workspace.ComposerText = "保留差异测试草稿";
        await WaitForStatisticsAsync(fixture.Workspace, fixture.Token, fixture.Services, fixture.TaskId);
        Assert.Equal("+1", fixture.Workspace.FileChanges.Statistics.AddedLabel);
        Assert.Equal("-1", fixture.Workspace.FileChanges.Statistics.RemovedLabel);
        await fixture.Workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        Assert.True(fixture.Workspace.FilePreview.Active!.IsReady, fixture.Workspace.FilePreview.Active.Status);
        Assert.Contains("-alpha", fixture.Workspace.FilePreview.Active.Text);
        Assert.Contains("+beta", fixture.Workspace.FilePreview.Active.Text);
        Assert.All(inputs.Sessions, item => { Assert.True(item.IsDisposed); Assert.False(File.Exists(item.Path)); });
        Assert.True(inputs.Sessions.Count >= 4);
        await AssertRestoredAsync(fixture);
        fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        fixture.Workspace.FileChanges.Undo.RequestCommand.Execute(null);
        await fixture.Workspace.FileChanges.Undo.ConfirmCommand.ExecuteAsync(null).WaitAsync(fixture.Token);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("保留差异测试草稿", fixture.Workspace.ComposerText);
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        Assert.All(fixture.Server.Requests, request => Assert.Contains("file-flow-model", request));
        Assert.Equal(3, fixture.Server.Requests.Count);
    }, TestContext.Current.CancellationToken);

    private static async Task WaitForStatisticsAsync(WorkspaceViewModel workspace, CancellationToken token, IServiceProvider services, Guid taskId)
    {
        while (!workspace.FileChanges.Statistics.HasTotals && !workspace.FileChanges.Statistics.HasError) await Task.Delay(15, token);
        var audits = await services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(taskId, 30, token);
        Assert.False(workspace.FileChanges.Statistics.HasError, string.Join("; ", audits.Select(item => item.ToolName + ":" + item.ErrorCode)));
    }

    [Fact]
    public Task RefusedInputIsVisibleInStatisticsReviewAndAuditAndManualRetryRecoversWithoutModelReplay() => _session.DispatchAsync(async () =>
    {
        var inputs = new ObservedDiffInputSessions { Creating = _ =>
            throw new FileMutationSessionException("安全测试拒绝临时输入", "diff_input_refused") };
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n", configure: services =>
            services.Replace(ServiceDescriptor.Singleton<IDiffInputFileSessionFactory>(inputs)));
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();
        fixture.Workspace.ComposerText = "保留失败后的草稿";
        while (!fixture.Workspace.FileChanges.Statistics.HasError
            || !fixture.Workspace.FileChanges.Statistics.RetryCommand.CanExecute(null)) await Task.Delay(15, fixture.Token);
        Assert.False(fixture.Workspace.FileChanges.Statistics.HasTotals);
        Assert.True(fixture.Workspace.FileChanges.Statistics.RetryCommand.CanExecute(null));
        await fixture.Workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        Assert.False(fixture.Workspace.FilePreview.Active!.IsReady);
        Assert.Contains("安全测试拒绝临时输入", fixture.Workspace.FilePreview.Active.Status);
        Assert.Empty(inputs.Sessions);
        var reader = fixture.Services.GetRequiredService<IToolExecutionAuditReader>();
        var audits = await reader.GetRecentAsync(fixture.TaskId, 30, fixture.Token);
        Assert.Contains(audits, item => item.ToolName == "filesystem.change_statistics" && item.ErrorCode == "diff_input_refused");
        Assert.Contains(audits, item => item.ToolName == "filesystem.review_change" && item.ErrorCode == "diff_input_refused");
        inputs.Creating = null;
        fixture.Workspace.FileChanges.Statistics.RetryCommand.Execute(null);
        await WaitForStatisticsAsync(fixture.Workspace, fixture.Token, fixture.Services, fixture.TaskId);
        await fixture.Workspace.FilePreview.RefreshCommand.ExecuteAsync(null);
        Assert.True(fixture.Workspace.FilePreview.Active.IsReady, fixture.Workspace.FilePreview.Active.Status);
        Assert.Contains("+beta", fixture.Workspace.FilePreview.Active.Text);
        Assert.All(inputs.Sessions, item => { Assert.True(item.IsDisposed); Assert.False(File.Exists(item.Path)); });
        Assert.Equal("保留失败后的草稿", fixture.Workspace.ComposerText);
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.Equal("beta\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
    }, TestContext.Current.CancellationToken);

    private static async Task AssertRestoredAsync(WorkspaceFileEditingFixture fixture)
    {
        await using var services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
        var workspace = services.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        Assert.False(workspace.IsRunning);
        await WaitForStatisticsAsync(workspace, fixture.Token, services, fixture.TaskId);
        await workspace.FileChanges.ReviewCommand.ExecuteAsync(null);
        Assert.True(workspace.FilePreview.Active!.IsReady, workspace.FilePreview.Active.Status);
        Assert.Contains("+beta", workspace.FilePreview.Active.Text);
        var audits = await services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 30, fixture.Token);
        Assert.Contains(audits, audit => audit.ToolName == "filesystem.change_statistics" && audit.IsSuccess == true && audit.CompletedAt is not null);
        Assert.Contains(audits, audit => audit.ToolName == "filesystem.review_change" && audit.IsSuccess == true && audit.CompletedAt is not null);
        Assert.Empty(Directory.GetFiles(services.GetRequiredService<ITaskArtifactPaths>().GetTaskDirectory(fixture.TaskId), "diff-input-*"));
    }
}
