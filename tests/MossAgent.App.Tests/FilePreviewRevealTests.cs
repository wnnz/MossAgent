using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewRevealTests
{
    [Fact]
    public async Task RevealUsesExactTaskPolicyAndPipeline_NotPlatformService()
    {
        using var fixture = new FilePreviewActionFixture(ApprovalPolicy.AskEveryTime);
        await fixture.OpenAsync();
        Assert.True(fixture.Pane.RevealCommand.CanExecute(null));
        await fixture.Pane.RevealCommand.ExecuteAsync(null);
        var call = Assert.Single(fixture.Calls, call => call.Request.ToolName == "filesystem.reveal_file");
        Assert.Equal(fixture.Context.TaskId, call.Context.TaskId);
        Assert.Equal(fixture.Context.AuthorizedRoots, call.Context.AuthorizedRoots);
        Assert.Equal(ApprovalPolicy.AskEveryTime, call.Context.ApprovalPolicy);
        Assert.Equal(TimeSpan.FromSeconds(30), call.Context.Timeout);
        Assert.Equal(fixture.Pane.Active!.Path, call.Request.Arguments.GetProperty("path").GetString());
    }

    [Fact]
    public async Task ReadOnlyAndStoredArtifactsCannotRevealOriginalFiles()
    {
        using var fixture = new FilePreviewActionFixture(ApprovalPolicy.ReadOnly);
        await fixture.OpenAsync();
        Assert.False(fixture.Pane.CanReveal);
        await fixture.Pane.RevealCommand.ExecuteAsync(null);
        Assert.DoesNotContain(fixture.Calls, call => call.Request.ToolName == "filesystem.reveal_file");
        fixture.Pane.Attach(fixture.Context with { ApprovalPolicy = ApprovalPolicy.FullAccess });
        await fixture.Pane.OpenArtifactAsync(new ToolArtifactRecord(Guid.NewGuid(), Guid.NewGuid(), fixture.Context.TaskId,
            "artifact.cs", "snapshot", "text/plain", 0, "hash", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        Assert.False(fixture.Pane.CanReveal);
    }

    [Theory]
    [InlineData("task")]
    [InlineData("tab")]
    [InlineData("hide")]
    [InlineData("close")]
    public async Task OwnerChangesCancelPendingRevealAndRejectLateNotice(string change)
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        fixture.Reveal = (_, _, token) => { captured = token; return pending.Task; };
        var operation = fixture.Pane.RevealCommand.ExecuteAsync(null);
        Assert.False(fixture.Pane.CanReveal);
        if (change == "task") fixture.Pane.Attach(fixture.Context with { TaskId = Guid.NewGuid() });
        else if (change == "tab") await fixture.OpenAsync("another.cs");
        else if (change == "hide") fixture.Pane.Hide();
        else fixture.Pane.CloseCommand.Execute(fixture.Pane.Active);
        Assert.True(captured.IsCancellationRequested);
        pending.SetResult(ToolResult.Success("late", string.Empty));
        await operation;
        Assert.Empty(fixture.Pane.Notice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReopenedTabCannotReceiveLateFailureOrCancellation(bool cancelled)
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reveal = (_, _, _) => pending.Task;
        var operation = fixture.Pane.RevealCommand.ExecuteAsync(null);
        fixture.Pane.Hide();
        fixture.Pane.SelectCommand.Execute(fixture.Pane.Active);
        Assert.False(fixture.Pane.CanReveal);
        pending.SetException(cancelled ? new OperationCanceledException() : new InvalidOperationException("secret"));
        await operation;
        Assert.Empty(fixture.Pane.Notice);
        Assert.True(fixture.Pane.CanReveal);
    }
}
