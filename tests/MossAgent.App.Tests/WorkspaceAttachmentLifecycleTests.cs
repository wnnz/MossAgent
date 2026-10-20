using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearReleasesUncooperativePortAndLateFailureCannotReplaceNewSelection(bool duringRead)
    {
        var project = Project();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);
        var adding = manager.AddFilesCommand.ExecuteAsync(project);

        manager.Clear();
        await adding.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(duringRead ? probe.ReaderCompletion.Task.IsCompleted : probe.PickerCompletion.Task.IsCompleted);
        Assert.True(duringRead ? probe.ReaderToken.IsCancellationRequested : probe.PickerToken.IsCancellationRequested);

        probe.HoldPicker = probe.HoldReader = false;
        probe.SetContent(project, "new-selection-only");
        await manager.AddFilesCommand.ExecuteAsync(project);
        var current = Assert.Single(manager.Items);
        var oldError = new InvalidOperationException("synthetic-old-error");
        if (duringRead) probe.ReaderCompletion.SetException(oldError);
        else probe.PickerCompletion.SetException(oldError);
        await Task.Yield();
        Assert.Same(current, Assert.Single(manager.Items));
        Assert.Contains("new-selection-only", manager.BuildContext());
        Assert.Empty(manager.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitCancellationCancelsBothStagesAndAllowsRetry(bool duringRead)
    {
        var project = Project();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);
        var adding = manager.AddFilesCommand.ExecuteAsync(project);

        Assert.True(manager.AddFilesCommand.CanBeCanceled);
        manager.AddFilesCommand.Cancel();
        await adding.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(duringRead ? probe.ReaderToken.IsCancellationRequested : probe.PickerToken.IsCancellationRequested);
        Assert.Empty(manager.Items);
        Assert.Equal(WorkspaceText.Get("AttachmentCancelled"), manager.Status);
        probe.HoldPicker = probe.HoldReader = false;
        await manager.AddFilesCommand.ExecuteAsync(project);
        Assert.Single(manager.Items);
        Assert.Empty(manager.Status);
        probe.Release();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutBoundsUncooperativePickerAndReaderWithRecoverableFeedback(bool duringRead)
    {
        var project = Project();
        var clock = new ManualToolTimeProvider();
        var probe = new WorkspaceAttachmentOperationProbe { HoldPicker = !duringRead, HoldReader = duringRead };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe, TimeSpan.FromMinutes(5), clock);
        manager.SetContext(project, null);
        var adding = manager.AddFilesCommand.ExecuteAsync(project);

        clock.Advance(TimeSpan.FromMinutes(5));
        await adding.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Empty(manager.Items);
        Assert.True(duringRead ? probe.ReaderToken.IsCancellationRequested : probe.PickerToken.IsCancellationRequested);
        Assert.Equal(WorkspaceText.Get("AttachmentTimedOut"), manager.Status);
        Assert.True(manager.AddFilesCommand.CanExecute(project));
        probe.Release();
    }

    [Theory]
    [InlineData("task")]
    [InlineData("project")]
    [InlineData("primary")]
    [InlineData("roots")]
    public async Task ContextChangesInvalidatePendingReadsEvenWhenItemsWouldOtherwiseBePreserved(string change)
    {
        var project = Project();
        var taskId = Guid.NewGuid();
        var probe = new WorkspaceAttachmentOperationProbe { HoldReader = true };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, taskId);
        var adding = manager.AddFilesCommand.ExecuteAsync(project);
        var next = change switch
        {
            "project" => project with { Id = Guid.NewGuid() },
            "primary" => project with { PrimaryDirectory = Path.Combine(project.PrimaryDirectory, "other") },
            "roots" => project with { AuthorizedDirectories = [Path.Combine(project.PrimaryDirectory, "other")] },
            _ => project
        };

        manager.SetContext(next, change == "task" ? Guid.NewGuid() : taskId, preserveItems: true);
        await adding.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        probe.Release();
        Assert.Empty(manager.Items);
        Assert.Empty(manager.Status);
        Assert.True(probe.ReaderToken.IsCancellationRequested);
        Assert.True(manager.AddFilesCommand.CanExecute(next));
        if (change != "task") Assert.False(manager.AddFilesCommand.CanExecute(project));
    }

    [Fact]
    public async Task MutatedAuthorizationListCannotChangeCapturedReaderScopeOrKeepAttachments()
    {
        var roots = new List<string> { Path.Combine(Path.GetTempPath(), "original-authorized") };
        var project = Project() with { AuthorizedDirectories = roots };
        var probe = new WorkspaceAttachmentOperationProbe { HoldReader = true };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);
        var adding = manager.AddFilesCommand.ExecuteAsync(project);

        roots.Clear();
        Assert.Single(probe.ReadProject!.AuthorizedDirectories);
        Assert.NotSame(roots, probe.ReadProject.AuthorizedDirectories);
        manager.SetContext(project, null, preserveItems: true);
        await adding.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(probe.ReaderToken.IsCancellationRequested);
        Assert.Empty(manager.Items);
        probe.Release();
    }

    [Fact]
    public async Task SameContextRefreshDoesNotCancelSelectionOrRemoveItems()
    {
        var project = Project();
        var taskId = Guid.NewGuid();
        var probe = new WorkspaceAttachmentOperationProbe { HoldReader = true };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, taskId);
        var adding = manager.AddFilesCommand.ExecuteAsync(project);
        manager.SetContext(project with { Name = "Renamed" }, taskId);
        Assert.False(probe.ReaderToken.IsCancellationRequested);
        probe.Release();
        await adding;
        var item = Assert.Single(manager.Items);
        manager.SetContext(project, taskId);
        Assert.Same(item, Assert.Single(manager.Items));
    }

    [Fact]
    public async Task ForeignProjectAndProjectlessCommandsNeverOpenPicker()
    {
        var project = Project();
        var foreign = project with { Id = Guid.NewGuid() };
        var probe = new WorkspaceAttachmentOperationProbe();
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);
        Assert.False(manager.AddFilesCommand.CanExecute(foreign));
        await manager.AddFilesCommand.ExecuteAsync(foreign);
        Assert.Equal(WorkspaceText.Get("AttachmentContextChanged"), manager.Status);
        manager.SetContext(null, null);
        Assert.False(manager.AddFilesCommand.CanExecute(null));
        await manager.AddFilesCommand.ExecuteAsync(null);
        Assert.Equal(WorkspaceText.Get("AttachmentProjectRequired"), manager.Status);
        Assert.Equal(0, probe.PickerCalls);
        Assert.Equal(0, probe.ReaderCalls);
    }

    internal static ProjectProfile Project() => new(Guid.NewGuid(), "Attachment scope",
        Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N")), [], DateTimeOffset.UtcNow);
}
