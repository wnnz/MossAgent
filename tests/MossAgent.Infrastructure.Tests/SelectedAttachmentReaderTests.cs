using System.Diagnostics;
using MossAgent.Application.Attachments;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SelectedAttachmentReaderTests
{
    [Fact]
    public async Task ReadsOnlyCapturedAbsoluteFilesWithoutCreatingProjectsOrDirectoryGrants()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var text = Path.Combine(fixture.Project.PrimaryDirectory, "selected.txt");
        var image = Path.Combine(fixture.Project.PrimaryDirectory, "selected.png");
        var sibling = Path.Combine(fixture.Project.PrimaryDirectory, "not-selected.txt");
        await File.WriteAllTextAsync(text, "explicitly selected text", fixture.Token);
        await File.WriteAllBytesAsync(image, AttachmentImageTestData.Static("image/png"), fixture.Token);
        await File.WriteAllTextAsync(sibling, "unselected sibling", fixture.Token);
        ISelectedAttachmentReader reader = new WorkspaceAttachmentReader(new SkiaAttachmentImageDecoder());
        var paths = new[] { text, image };
        var reading = reader.ReadSelectedAsync(paths, fixture.Token);
        paths[0] = sibling;
        var selected = await reading;
        Assert.Equal(new[] { text, image }, selected.Select(item => item.FullPath).ToArray());
        Assert.Equal("explicitly selected text", selected[0].Content);
        Assert.Equal("image/png", selected[1].MediaType);
        Assert.Equal(new[] { "selected.txt", "selected.png" }, selected.Select(item => item.DisplayPath).ToArray());
        Assert.DoesNotContain(selected, item => item.Content.Contains("unselected sibling", StringComparison.Ordinal));
        Assert.Equal(fixture.Project.Id, Assert.Single(await fixture.Repository.GetProjectsAsync(fixture.Token)).Id);
        Assert.Empty(await reader.ReadSelectedAsync([], fixture.Token));
    }

    [Theory]
    [InlineData("relative.txt")]
    [InlineData("../relative.txt")]
    [InlineData("C:\\selected.txt:hidden")]
    [InlineData("\\\\.\\PhysicalDrive0")]
    [InlineData("\\\\?\\GLOBALROOT\\Device\\HarddiskVolumeShadowCopy1\\file.txt")]
    public async Task RelativeStreamsAndDevicePathsAreNotExplicitRegularFileSelections(string path)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new WorkspaceAttachmentReader().ReadSelectedAsync([path], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectedFileAncestryCannotFollowAJunctionEvenWhenAProjectWouldAuthorizeItsTarget()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var target = Path.GetFullPath(Path.Combine(fixture.Project.PrimaryDirectory, "junction-target"));
        var link = Path.GetFullPath(Path.Combine(fixture.Project.PrimaryDirectory, "selected-junction"));
        var root = Path.GetFullPath(fixture.Project.PrimaryDirectory) + Path.DirectorySeparatorChar;
        Assert.StartsWith(root, target, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(root, link, StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "selected.txt"), "linked data", fixture.Token);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync(fixture.Token);
        Assert.Equal(0, process.ExitCode);
        try
        {
            var reader = new WorkspaceAttachmentReader();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadSelectedAsync([Path.Combine(link, "selected.txt")], fixture.Token));
            Assert.Equal("linked data", Assert.Single(await reader.ReadAsync(fixture.Project, [Path.Combine(link, "selected.txt")], fixture.Token)).Content);
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndThirtySecondTimeoutReturnNoSelection(bool timeout)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "selected.txt");
        await File.WriteAllTextAsync(path, "retained", fixture.Token);
        var clock = timeout ? new ExpiringDraftTimeProvider() : null;
        var reader = new WorkspaceAttachmentReader(timeProvider: clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        if (!timeout) await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadSelectedAsync([path], cancellation.Token));
        if (clock is not null) Assert.Equal(TimeSpan.FromSeconds(30), clock.ObservedTimeout);
        Assert.Equal("retained", await File.ReadAllTextAsync(path, fixture.Token));
    }

    [Fact]
    public async Task CountAndTextByteLimitsStillApplyWithoutAProject()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "large.txt");
        await File.WriteAllTextAsync(path, new string('a', WorkspaceAttachmentLimits.MaximumBytesPerFile + 1), fixture.Token);
        var reader = new WorkspaceAttachmentReader();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadSelectedAsync([path], fixture.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadSelectedAsync(Enumerable.Repeat(path, 11).ToArray(), fixture.Token));
    }
}
