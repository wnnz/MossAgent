using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceArtifactRecoveryViewModelTests
{
    private static long _id;

    private static ProjectProfile Project(string name) => new(Guid.NewGuid(), name,
        Path.Combine(Path.GetTempPath(), name), [], DateTimeOffset.UtcNow);

    private static ArtifactSessionRecord Record(ArtifactSessionKind kind = ArtifactSessionKind.Artifact,
        string? error = "staged", Guid? taskId = null) =>
        new(++_id, taskId ?? Guid.NewGuid(), $"edit-{Guid.NewGuid():N}.txt",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), kind,
            ArtifactSessionStage.Orphaned, null, error,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task UnresolvedRecordsLoadWithLocalizedSummaryDetailAndTitle()
    {
        var project = Project("a");
        var staged = Record();
        var completed = Record(error: "completed");
        var journal = new RecordingArtifactSessionJournal();
        journal.SetResults(project.Id, staged, completed);
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);

        viewModel.SetProject(project);
        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Contains("暂存残留", viewModel.Items[0].Summary);
        Assert.Contains("任务产物", viewModel.Items[0].Summary);
        Assert.Contains("已提交未保留", viewModel.Items[1].Summary);
        Assert.Equal(staged.FileName, viewModel.Items[0].FileName);
        Assert.Contains(staged.TaskId.ToString("N")[..8], viewModel.Items[0].Detail);
        Assert.Equal("产物文件待核实（2）", viewModel.Title);
        Assert.True(viewModel.IsVisible);
        Assert.False(viewModel.HasNotice);
        Assert.Equal("标记已核实", viewModel.Items[0].AcknowledgeLabel);
    }

    [Fact]
    public async Task UnavailableJournalOrMissingProjectStaysHiddenAndNeverReads()
    {
        using var missingPort = new WorkspaceArtifactRecoveryViewModel(null);
        missingPort.SetProject(Project("a"));
        await missingPort.RefreshAsync();
        Assert.False(missingPort.IsAvailable);
        Assert.False(missingPort.IsVisible);
        Assert.False(missingPort.RefreshCommand.CanExecute(null));

        var journal = new RecordingArtifactSessionJournal();
        using var noProject = new WorkspaceArtifactRecoveryViewModel(journal);
        await noProject.RefreshAsync();
        Assert.False(noProject.IsVisible);
        Assert.Empty(journal.ReadCalls);
    }

    [Fact]
    public async Task AcknowledgeCallsJournalOnceAndRemovesOnlyThatRow()
    {
        var project = Project("a");
        var first = Record();
        var second = Record(error: "cleanup_unknown");
        var journal = new RecordingArtifactSessionJournal();
        journal.SetResults(project.Id, first, second);
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);
        viewModel.SetProject(project);

        var row = viewModel.Items[0];
        Assert.True(row.AcknowledgeCommand.CanExecute(null));
        await row.AcknowledgeCommand.ExecuteAsync(null);

        Assert.Equal([first.Id], journal.Acknowledged);
        var remaining = Assert.Single(viewModel.Items);
        Assert.Equal(second.Id, remaining.RecordId);
        Assert.Contains("清理未确认", remaining.Summary);
    }

    [Fact]
    public async Task AcknowledgeStaleShowsNoticeAndRereads()
    {
        var project = Project("a");
        var record = Record();
        var journal = new RecordingArtifactSessionJournal
        { AcknowledgeFailure = new ArtifactSessionStaleException() };
        journal.SetResults(project.Id, record);
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);
        viewModel.SetProject(project);
        Assert.Single(viewModel.Items);
        var reads = journal.ReadCalls.Count;

        await viewModel.Items[0].AcknowledgeCommand.ExecuteAsync(null);

        Assert.Empty(journal.Acknowledged);
        Assert.Equal("记录状态已变化（可能已在其他窗口处理），已重新读取。", viewModel.Notice);
        Assert.Equal(reads + 1, journal.ReadCalls.Count);
        var row = Assert.Single(viewModel.Items);
        Assert.Equal(record.Id, row.RecordId);
    }

    [Fact]
    public async Task ReadFailureShowsLocalizedNoticeWithoutExceptionDetail()
    {
        var journal = new RecordingArtifactSessionJournal
        { ReadFailure = new InvalidOperationException("synthetic-recovery-secret") };
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);
        viewModel.SetProject(Project("a"));
        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasNotice);
        Assert.Equal("无法读取产物会话记录，请刷新重试。", viewModel.Notice);
        Assert.DoesNotContain("synthetic-recovery-secret", viewModel.Notice);
        Assert.True(viewModel.IsVisible);
    }

    [Fact]
    public async Task DiffInputAndUnverifiedRecordsUseKindAndStageLabels()
    {
        var project = Project("a");
        var diff = Record(ArtifactSessionKind.DiffInput, "cleanup_unknown");
        var unverified = Record(error: "unverified");
        var journal = new RecordingArtifactSessionJournal();
        journal.SetResults(project.Id, diff, unverified);
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);

        viewModel.SetProject(project);
        await viewModel.RefreshAsync();

        Assert.Contains("差异输入", viewModel.Items[0].Summary);
        Assert.Contains("清理未确认", viewModel.Items[0].Summary);
        Assert.Contains("路径复核未通过", viewModel.Items[1].Summary);
    }

    [Fact]
    public async Task SdkTempRecordsUseKindAndStageLabels()
    {
        var project = Project("a");
        var sdkTemp = Record(ArtifactSessionKind.ExternalSdkTemp, "sdk_temp");
        var journal = new RecordingArtifactSessionJournal();
        journal.SetResults(project.Id, sdkTemp);
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);

        viewModel.SetProject(project);
        await viewModel.RefreshAsync();

        Assert.Contains("浏览器暂存未确认", viewModel.Items[0].Summary);
        Assert.Contains("浏览器下载暂存", viewModel.Items[0].Summary);
        Assert.Equal(sdkTemp.FileName, viewModel.Items[0].FileName);
    }

    [Fact]
    public async Task ProjectSwitchClearsItemsAndStopsLoadingTheOldProject()
    {
        var journal = new RecordingArtifactSessionJournal();
        var projectA = Project("a");
        var projectB = Project("b");
        journal.SetResults(projectA.Id, Record());
        journal.SetResults(projectB.Id, Record(error: "completed"));
        using var viewModel = new WorkspaceArtifactRecoveryViewModel(journal);

        viewModel.SetProject(projectA);
        await viewModel.RefreshAsync();
        Assert.Single(viewModel.Items);

        viewModel.SetProject(projectB);
        await viewModel.RefreshAsync();

        var row = Assert.Single(viewModel.Items);
        Assert.Contains("已提交未保留", row.Summary);
        Assert.Equal([projectA.Id, projectA.Id, projectB.Id, projectB.Id], journal.ReadCalls);
    }
}
