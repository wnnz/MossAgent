using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeRecoveryViewModelTests
{
    private static ProjectProfile Project(string name) => new(Guid.NewGuid(), name,
        Path.Combine(Path.GetTempPath(), name), [], DateTimeOffset.UtcNow);

    private static WorktreeCreationIntent Intent(Guid projectId, WorktreeCreationIntentStage stage, string? error) =>
        new(Guid.NewGuid(), projectId, @"D:\repo", null, "0123456789abcdef",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), stage, error,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static WorktreeCleanupIntent Cleanup(Guid projectId, WorktreeCleanupIntentStage stage, string? error) =>
        new(Guid.NewGuid(), projectId, @"D:\repo",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), stage, error,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task IncompleteIntentsLoadWithLocalizedSummaryAndTitle()
    {
        var project = Project("a");
        var orphan = Intent(project.Id, WorktreeCreationIntentStage.Orphaned, "missing");
        var failed = Intent(project.Id, WorktreeCreationIntentStage.Failed, "git");
        var intents = new RecordingWorktreeCreationIntents();
        intents.SetResults(project.Id, orphan, failed);
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents);

        viewModel.SetProject(project);
        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Contains("目标目录不存在", viewModel.Items[0].Summary);
        Assert.Contains("Git 创建失败", viewModel.Items[1].Summary);
        Assert.Equal("工作树创建待核实（2）", viewModel.Title);
        Assert.True(viewModel.IsVisible);
        Assert.False(viewModel.HasNotice);
    }

    [Fact]
    public async Task UnavailablePortOrMissingProjectStaysHiddenAndNeverReads()
    {
        using var missingPort = new WorkspaceWorktreeRecoveryViewModel(null);
        missingPort.SetProject(Project("a"));
        await missingPort.RefreshAsync();
        Assert.False(missingPort.IsAvailable);
        Assert.False(missingPort.IsVisible);
        Assert.False(missingPort.RefreshCommand.CanExecute(null));

        var unsupported = new RecordingWorktreeCreationIntents { SupportsCreationIntents = false };
        using var disabled = new WorkspaceWorktreeRecoveryViewModel(unsupported);
        disabled.SetProject(Project("b"));
        await disabled.RefreshAsync();
        Assert.False(disabled.IsVisible);
        Assert.Empty(unsupported.ReadCalls);

        var recording = new RecordingWorktreeCreationIntents();
        using var noProject = new WorkspaceWorktreeRecoveryViewModel(recording);
        await noProject.RefreshAsync();
        Assert.False(noProject.IsVisible);
        Assert.Empty(recording.ReadCalls);
    }

    [Fact]
    public async Task AcknowledgeCallsPortOnceAndRemovesOnlyThatRow()
    {
        var project = Project("a");
        var first = Intent(project.Id, WorktreeCreationIntentStage.Orphaned, "missing");
        var second = Intent(project.Id, WorktreeCreationIntentStage.Failed, "io");
        var intents = new RecordingWorktreeCreationIntents();
        intents.SetResults(project.Id, first, second);
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents);
        viewModel.SetProject(project);

        var row = viewModel.Items[0];
        Assert.True(row.AcknowledgeCommand.CanExecute(null));
        await row.AcknowledgeCommand.ExecuteAsync(null);

        Assert.Equal([first.TaskId], intents.Acknowledged);
        var remaining = Assert.Single(viewModel.Items);
        Assert.Equal(second.TaskId, remaining.TaskId);
    }

    [Fact]
    public async Task AcknowledgeStaleShowsNoticeAndRereads()
    {
        var project = Project("a");
        var intent = Intent(project.Id, WorktreeCreationIntentStage.Orphaned, "missing");
        var intents = new RecordingWorktreeCreationIntents
        {
            AcknowledgeFailure = new WorktreeAuthorizationException(),
        };
        intents.SetResults(project.Id, intent);
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents);
        viewModel.SetProject(project);
        Assert.Single(viewModel.Items);
        var reads = intents.ReadCalls.Count;

        await viewModel.Items[0].AcknowledgeCommand.ExecuteAsync(null);

        Assert.Empty(intents.Acknowledged);
        Assert.Equal("记录状态已变化（可能已在其他窗口处理），已重新读取。", viewModel.Notice);
        Assert.Equal(reads + 1, intents.ReadCalls.Count);
        var row = Assert.Single(viewModel.Items);
        Assert.Equal(intent.TaskId, row.TaskId);
    }

    [Fact]
    public async Task ReadFailureShowsLocalizedNoticeWithoutExceptionDetail()
    {
        var intents = new RecordingWorktreeCreationIntents
        {
            ReadFailure = new InvalidOperationException("synthetic-recovery-secret"),
        };
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents);
        viewModel.SetProject(Project("a"));
        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasNotice);
        Assert.Equal("无法读取工作树创建记录，请刷新重试。", viewModel.Notice);
        Assert.DoesNotContain("synthetic-recovery-secret", viewModel.Notice);
        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public async Task ProjectSwitchDropsResultsFromSupersededRead()
    {
        var intents = new RecordingWorktreeCreationIntents();
        var projectA = Project("a");
        var projectB = Project("b");
        var intentA = Intent(projectA.Id, WorktreeCreationIntentStage.Orphaned, "missing");
        var intentB = Intent(projectB.Id, WorktreeCreationIntentStage.Orphaned, "untracked");
        intents.SetResults(projectA.Id, intentA);
        intents.SetResults(projectB.Id, intentB);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        intents.ReadGate = gate;
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents);

        viewModel.SetProject(projectA);
        await intents.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Empty(viewModel.Items);

        intents.ReadGate = null;
        viewModel.SetProject(projectB);
        gate.TrySetResult();

        var row = Assert.Single(viewModel.Items);
        Assert.Equal(intentB.TaskId, row.TaskId);
        Assert.Contains("目录存在但没有 Git 指针", row.Summary);
        Assert.Equal([projectA.Id, projectB.Id], intents.ReadCalls);
    }

    [Fact]
    public async Task CleanupIntentsListAndAcknowledgeRoutesToCleanupPort()
    {
        var project = Project("a");
        var creation = Intent(project.Id, WorktreeCreationIntentStage.Orphaned, "missing");
        var cleanup = Cleanup(project.Id, WorktreeCleanupIntentStage.Orphaned, "retained");
        var intents = new RecordingWorktreeCreationIntents();
        var cleanupIntents = new RecordingWorktreeCleanupIntents();
        intents.SetResults(project.Id, creation);
        cleanupIntents.SetResults(project.Id, cleanup);
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents, cleanupIntents);
        viewModel.SetProject(project);
        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.Items.Count);
        var row = viewModel.Items[1];
        Assert.Equal(cleanup.TaskId, row.TaskId);
        Assert.Contains("清理中断", row.Summary);
        Assert.Contains("目标目录仍存在", row.Summary);
        Assert.Contains("D:\\repo", row.Detail);

        await row.AcknowledgeCommand.ExecuteAsync(null);

        Assert.Equal([cleanup.TaskId], cleanupIntents.Acknowledged);
        Assert.Empty(intents.Acknowledged);
        var remaining = Assert.Single(viewModel.Items);
        Assert.Equal(creation.TaskId, remaining.TaskId);
    }

    [Fact]
    public async Task CleanupFailureUsesRemovalLabelAndCleanupPortAloneKeepsPanelAvailable()
    {
        var project = Project("a");
        var cleanup = Cleanup(project.Id, WorktreeCleanupIntentStage.Failed, "git");
        var cleanupIntents = new RecordingWorktreeCleanupIntents();
        cleanupIntents.SetResults(project.Id, cleanup);
        using var viewModel = new WorkspaceWorktreeRecoveryViewModel(null, cleanupIntents);

        Assert.True(viewModel.IsAvailable);
        viewModel.SetProject(project);
        await viewModel.RefreshAsync();

        var row = Assert.Single(viewModel.Items);
        Assert.True(row.IsCleanupItem);
        Assert.Contains("清理失败", row.Summary);
        Assert.Contains("Git 删除失败", row.Summary);
    }

    [Fact]
    public async Task StopAsyncCancelsGatedReadWithoutException()
    {
        var intents = new RecordingWorktreeCreationIntents
        {
            ReadGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var viewModel = new WorkspaceWorktreeRecoveryViewModel(intents);
        var project = Project("a");
        viewModel.SetProject(project);
        await intents.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await viewModel.StopAsync();

        var token = Assert.Single(intents.ReadTokens);
        Assert.True(token.IsCancellationRequested);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.HasNotice);
    }
}
