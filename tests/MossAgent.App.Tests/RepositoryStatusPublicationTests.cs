using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RepositoryStatusPublicationTests
{
    [Fact]
    public async Task PendingSelection_DoesNotDisplayThePreviousProjectsBranch()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        var first = status.RefreshAsync(Project("first"), null);
        probe.Reads[0].Completion.SetResult("first-branch");
        await first;
        var second = status.RefreshAsync(Project("second"), null);
        Assert.Equal("second", status.DirectoryLabel);
        Assert.Equal("Git 状态未知", status.BranchLabel);
        probe.Reads[1].Completion.SetResult("second-branch");
        await second;
        Assert.Equal("second-branch", status.BranchLabel);
        await status.DisposeAsync();
    }

    [Fact]
    public async Task ClosingDuringPublication_DoesNotLaunchALateRead()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        status.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(status.DirectoryLabel)) status.Dispose();
        };
        await status.RefreshAsync(Project("closing"), null);
        await status.DisposeAsync();
        Assert.Empty(probe.Reads);
    }

    [Fact]
    public async Task ReentrantSelection_DoesNotLaunchTheAlreadyStaleRead()
    {
        using var probe = new RepositoryStatusProbe();
        var status = new WorkspaceRepositoryStatusViewModel(probe);
        Task? selected = null;
        status.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(status.DirectoryLabel) && status.DirectoryLabel == "old")
                selected = status.RefreshAsync(Project("current"), null);
        };
        await status.RefreshAsync(Project("old"), null);
        var read = Assert.Single(probe.Reads);
        Assert.Equal(Project("current").PrimaryDirectory, read.Directory);
        read.Completion.SetResult("current-branch");
        await Assert.IsAssignableFrom<Task>(selected);
        Assert.Equal("current", status.DirectoryLabel);
        Assert.Equal("current-branch", status.BranchLabel);
        await status.DisposeAsync();
    }

    private static ProjectProfile Project(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), name);
        return new(Guid.NewGuid(), name, directory, [directory], DateTimeOffset.UtcNow);
    }
}
