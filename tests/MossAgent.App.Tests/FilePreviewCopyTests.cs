using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewCopyTests
{
    [Fact]
    public async Task CommandsCopyAuthorizedSnapshotAndExactPaths_WithoutReadingAgain()
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var payloads = new List<string>();
        var commands = new FilePreviewCopyCommands(() => true, text => { payloads.Add(text); return Task.CompletedTask; });
        commands.Attach(fixture.Pane);
        try
        {
            await commands.CopyContentCommand.ExecuteAsync(null);
            await commands.CopyPathCommand.ExecuteAsync(null);
            await commands.CopyRelativePathCommand.ExecuteAsync(null);
            Assert.Equal([fixture.Pane.Active!.Text, fixture.Pane.Active.Path, "..notes.cs"], payloads);
            Assert.Single(fixture.Calls);
            Assert.Contains("复制", commands.Notice);
            Assert.Contains(" > ..notes.cs", fixture.Pane.Active.DisplayPath);
        }
        finally { commands.Attach(null); }
    }

    [Fact]
    public async Task NoClipboardHiddenLoadingAndDetachedOwnersCannotCopy()
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var available = false;
        var payloads = new List<string>();
        var commands = new FilePreviewCopyCommands(() => available, text => { payloads.Add(text); return Task.CompletedTask; });
        commands.Attach(fixture.Pane);
        Assert.False(commands.CopyDefaultCommand.CanExecute(null));
        await commands.CopyDefaultCommand.ExecuteAsync(null);
        available = true;
        fixture.Pane.Active!.IsLoading = true;
        Assert.False(commands.CopyPathCommand.CanExecute(null));
        fixture.Pane.Active.IsLoading = false;
        fixture.Pane.Hide();
        Assert.False(commands.CopyPathCommand.CanExecute(null));
        commands.Attach(null);
        Assert.False(commands.CanShowMenu);
        Assert.Empty(payloads);
    }

    [Fact]
    public async Task PendingClipboardWriteBlocksNewWritesAndCannotPublishToNewScope()
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new FilePreviewCopyCommands(() => true, _ => completion.Task);
        commands.Attach(fixture.Pane);
        var pending = commands.CopyPathCommand.ExecuteAsync(null);
        fixture.Pane.Attach(fixture.Context with { TaskId = Guid.NewGuid() });
        await fixture.OpenAsync("new.cs");
        Assert.False(commands.CopyContentCommand.CanExecute(null));
        Assert.False(commands.CanShowMenu);
        completion.SetResult();
        await pending;
        Assert.Empty(commands.Notice);
        Assert.True(commands.CopyContentCommand.CanExecute(null));
        commands.Attach(null);
    }

    [Fact]
    public async Task CopyFailureIsSafe_OutsideWorkingDirectoryHasNoFakeRelativePath()
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync(Path.GetFullPath("C:\\separate-authorized-root\\file.cs"));
        var commands = new FilePreviewCopyCommands(() => true, _ => throw new InvalidOperationException("secret-detail"));
        commands.Attach(fixture.Pane);
        try
        {
            Assert.False(commands.CopyRelativePathCommand.CanExecute(null));
            await commands.CopyContentCommand.ExecuteAsync(null);
            Assert.Contains("失败", commands.Notice);
            Assert.DoesNotContain("secret-detail", commands.Notice);
        }
        finally { commands.Attach(null); }
    }
}
