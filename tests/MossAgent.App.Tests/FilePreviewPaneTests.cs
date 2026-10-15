using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewPaneTests
{
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), System.IO.Path.GetTempPath(), [System.IO.Path.GetTempPath()], ApprovalPolicy.ReadOnly);
    private static ToolResult Snapshot() => new(true, "snapshot", Artifacts: [new ToolArtifact("sample.cs", "snapshot", "text/plain", 0, "hash")]);

    [Fact]
    public async Task EquivalentReattachment_PreservesTabsButChangedApprovalClearsThem()
    {
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(Snapshot()) }, new PreviewProbeArtifactReader());
        var context = Context();
        pane.Attach(context);
        await pane.OpenAsync("file.cs", TestContext.Current.CancellationToken);
        var tab = pane.Active;
        pane.Attach(context with { AuthorizedRoots = context.AuthorizedRoots.ToArray() });
        Assert.Same(tab, pane.Active);
        Assert.Single(pane.Tabs);
        pane.Attach(context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime });
        Assert.Empty(pane.Tabs);
    }

    [Fact]
    public async Task CancelledNonCooperativeRequest_HasExplicitRetryableState()
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task }, new PreviewProbeArtifactReader());
        pane.Attach(Context());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var opening = pane.OpenAsync("cancelled.cs", cancellation.Token);
        cancellation.Cancel();
        pending.SetResult(Snapshot());
        await opening;
        Assert.False(pane.Active!.IsLoading);
        Assert.False(pane.Active.IsReady);
        Assert.Contains("取消", pane.Active.Status);
        Assert.True(pane.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task Open_ReusesPipelineExactContextAndDeduplicatesTabs()
    {
        var calls = new List<ToolRequest>();
        var context = Context();
        var tools = new InspectorProbeToolExecutor { Execute = (request, execution, _) =>
        {
            Assert.Equal(context.TaskId, execution.TaskId);
            Assert.Equal(context.ApprovalPolicy, execution.ApprovalPolicy);
            Assert.Equal(TimeSpan.FromSeconds(30), execution.Timeout);
            calls.Add(request);
            return Task.FromResult(Snapshot());
        } };
        using var pane = new FilePreviewPaneViewModel(tools, new PreviewProbeArtifactReader());
        Assert.False(pane.OpenCommand.CanExecute("sample.cs"));
        pane.Attach(context);
        await pane.OpenAsync("sample.cs", TestContext.Current.CancellationToken);
        await pane.OpenAsync("./sample.cs", TestContext.Current.CancellationToken);
        Assert.Single(pane.Tabs);
        Assert.Equal("filesystem.preview_file", Assert.Single(calls).ToolName);
        Assert.Contains("class Preview", pane.Active!.Text);
        Assert.True(pane.IsOpen);
        pane.Hide();
        Assert.False(pane.IsOpen);
        Assert.Single(pane.Tabs);
    }

    [Fact]
    public async Task SwitchingTask_CancelsAndRejectsLateNonCooperativeRead()
    {
        var pending = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        var reader = new PreviewProbeArtifactReader { Read = (_, _, token) => { captured = token; return pending.Task; } };
        var tools = new InspectorProbeToolExecutor { Execute = (_, _, _) => Task.FromResult(Snapshot()) };
        using var pane = new FilePreviewPaneViewModel(tools, reader);
        pane.Attach(Context());
        var opening = pane.OpenAsync("old.cs", TestContext.Current.CancellationToken);
        Assert.True(pane.Active!.IsLoading);
        pane.Attach(Context());
        Assert.True(captured.IsCancellationRequested);
        pending.SetResult(Encoding.UTF8.GetBytes("secret old result"));
        await opening;
        Assert.Empty(pane.Tabs);
        Assert.Null(pane.Active);
        Assert.False(pane.IsOpen);
    }

    [Fact]
    public async Task ClosePendingTab_RejectsLateResultAndReentry()
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor { Execute = (_, _, _) => pending.Task }, new PreviewProbeArtifactReader());
        pane.Attach(Context());
        var opening = pane.OpenAsync("old.cs", TestContext.Current.CancellationToken);
        var tab = pane.Active!;
        pane.CloseCommand.Execute(tab);
        Assert.False(pane.CloseCommand.CanExecute(tab));
        pending.SetResult(Snapshot());
        await opening;
        Assert.Empty(pane.Tabs);
        Assert.Equal(string.Empty, tab.Text);
        Assert.False(pane.IsOpen);
    }

    [Fact]
    public async Task TabsAreBoundedAndFailuresNeverExposeRawExceptions()
    {
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor { Execute = (_, _, _) => throw new IOException("secret-api-key") }, new PreviewProbeArtifactReader());
        pane.Attach(Context());
        for (var index = 0; index < FilePreviewPaneViewModel.MaximumTabs + 1; index++)
            await pane.OpenAsync($"file-{index}.cs", TestContext.Current.CancellationToken);
        Assert.Equal(FilePreviewPaneViewModel.MaximumTabs, pane.Tabs.Count);
        Assert.NotEmpty(pane.Notice);
        Assert.DoesNotContain("secret", pane.Active!.Status);
        pane.Dispose();
        Assert.False(pane.OpenCommand.CanExecute("file.cs"));
        await pane.OpenAsync("after.cs", TestContext.Current.CancellationToken);
        Assert.Empty(pane.Tabs);
    }

    [Fact]
    public async Task ForeignArtifact_IsRejectedWithoutRead()
    {
        var reads = 0;
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor(), new PreviewProbeArtifactReader
        { Read = (_, _, _) => { reads++; return Task.FromResult(Array.Empty<byte>()); } });
        pane.Attach(Context());
        var artifact = new ToolArtifactRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "foreign", "foreign", "text/plain", 0, "hash", DateTimeOffset.UtcNow);
        Assert.False(pane.OpenArtifactCommand.CanExecute(artifact));
        await pane.OpenArtifactAsync(artifact, TestContext.Current.CancellationToken);
        Assert.Equal(0, reads);
        Assert.Empty(pane.Tabs);
    }
}
