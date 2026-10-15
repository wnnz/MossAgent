using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DiffReviewSelectionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static ToolExecutionContext Context() => new(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.ReadOnly);
    private static ToolResult Status() => ToolResult.Success("status", "MM first.cs\0MM second.cs\0");

    [Fact]
    public Task RefreshRetainsTheSelectedPathAndRebindsTheSnapshot() => _session.DispatchAsync(async () =>
    {
        var executor = new InspectorProbeToolExecutor
        { Execute = (request, _, _) => Task.FromResult(request.ToolName == "git.status" ? Status() : ToolResult.Success("diff", "content")) };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        var previous = pane.Files[1];
        pane.SelectedFile = previous;
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("second.cs", pane.SelectedFile!.Path);
        Assert.Same(pane.Files[1], pane.SelectedFile);
        Assert.NotSame(previous, pane.SelectedFile);
        pane.IsStaged = true;
        Assert.Equal("second.cs", pane.SelectedFile.Path);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SelectionChangedDuringStatusReadWinsOverTheInitialSelection() => _session.DispatchAsync(async () =>
    {
        var pending = new TaskCompletionSource<ToolResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusReads = 0;
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) => request.ToolName == "git.status"
                ? ++statusReads == 1 ? Task.FromResult(Status()) : pending.Task
                : Task.FromResult(ToolResult.Success("diff", "content"))
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        var reading = pane.RefreshCommand.ExecuteAsync(null);
        pane.SelectedFile = pane.Files[1];
        pending.SetResult(Status());
        await reading;
        Assert.Equal("second.cs", pane.SelectedFile!.Path);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UnlistedSelectionCannotInitiateAnotherToolRequest() => _session.DispatchAsync(async () =>
    {
        var requests = new List<ToolRequest>();
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) =>
            {
                requests.Add(request);
                return Task.FromResult(request.ToolName == "git.status" ? Status() : ToolResult.Success("diff", "content"));
            }
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        var current = pane.SelectedFile;
        var count = requests.Count;
        pane.SelectedFile = new GitChangedFileViewModel("outside.cs", " M", false, true);
        Assert.Same(current, pane.SelectedFile);
        Assert.Equal(count, requests.Count);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UntrackedSelectionUsesTheStandardDiffToolWithExplicitMode() => _session.DispatchAsync(async () =>
    {
        ToolRequest? diff = null;
        var executor = new InspectorProbeToolExecutor
        {
            Execute = (request, _, _) =>
            {
                if (request.ToolName == "git.status") return Task.FromResult(ToolResult.Success("status", "?? added.cs\0"));
                diff = request;
                return Task.FromResult(ToolResult.Success("diff", "+new-content"));
            }
        };
        using var pane = new DiffPaneViewModel(executor);
        pane.Attach(Context());
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.NotNull(diff);
        Assert.Equal("git.diff", diff.ToolName);
        Assert.True(diff.Arguments.GetProperty("untracked").GetBoolean());
        Assert.Equal("+new-content", pane.Content);
    }, TestContext.Current.CancellationToken);
}
