using MossAgent.App.ViewModels;
using MossAgent.Application.Attachments;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceAttachmentLimitTests
{
    [Fact]
    public async Task DuplicatePathsAreNormalizedBeforeReadingAndExistingItemsAreNotDuplicated()
    {
        var project = WorkspaceAttachmentLifecycleTests.Project();
        var probe = new WorkspaceAttachmentOperationProbe();
        probe.SetContent(project);
        var path = probe.Paths[0];
        probe.Paths = [path, path.ToUpperInvariant(), Path.Combine(project.PrimaryDirectory, ".", "attachment.txt")];
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);

        await manager.AddFilesCommand.ExecuteAsync(project);
        Assert.Equal(path, Assert.Single(probe.ReadPaths));
        var attachment = Assert.Single(manager.Items);
        await manager.AddFilesCommand.ExecuteAsync(project);
        Assert.Equal(1, probe.ReaderCalls);
        Assert.Same(attachment, Assert.Single(manager.Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FileCountLimitsRejectOversizedPickerAndReaderResultsWithoutPartialPublication(bool oversizedReader)
    {
        var project = WorkspaceAttachmentLifecycleTests.Project();
        var probe = new WorkspaceAttachmentOperationProbe();
        probe.SetContent(project);
        if (oversizedReader)
            probe.Contents = Enumerable.Repeat(probe.Contents[0], WorkspaceAttachmentLimits.MaximumFiles + 1).ToArray();
        else
            probe.Paths = Enumerable.Range(0, WorkspaceAttachmentLimits.MaximumFiles + 1)
                .Select(index => Path.Combine(project.PrimaryDirectory, $"{index}.txt")).ToArray();
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);

        await manager.AddFilesCommand.ExecuteAsync(project);

        Assert.Equal(oversizedReader ? 1 : 0, probe.ReaderCalls);
        Assert.Empty(manager.Items);
        Assert.Contains(WorkspaceAttachmentLimits.MaximumFiles.ToString(), manager.Status);
        Assert.True(manager.AddFilesCommand.CanExecute(project));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpectedFileFailuresUseLocalizedGuidanceWithoutExceptionDetails(bool unauthorized)
    {
        var project = WorkspaceAttachmentLifecycleTests.Project();
        var probe = new WorkspaceAttachmentOperationProbe
        {
            ReaderError = unauthorized ? new UnauthorizedAccessException("synthetic-private-path")
                : new FileNotFoundException("synthetic-private-path")
        };
        probe.SetContent(project);
        using var manager = new WorkspaceAttachmentManagerViewModel(probe, probe);
        manager.SetContext(project, null);

        await manager.AddFilesCommand.ExecuteAsync(project);

        Assert.Equal(WorkspaceText.Get(unauthorized ? "AttachmentUnauthorized" : "AttachmentMissing"), manager.Status);
        Assert.DoesNotContain("synthetic-private-path", manager.Status);
        Assert.Empty(manager.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidTimeoutsAreRejectedAtConstruction(int seconds)
    {
        var probe = new WorkspaceAttachmentOperationProbe();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceAttachmentManagerViewModel(probe, probe, TimeSpan.FromSeconds(seconds)));
    }
}
