using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewPickerTests
{
    [Fact]
    public async Task SelectionUsesExactTaskContextAndDeduplicatesWithoutRereading()
    {
        var context = Context();
        var calls = new List<ToolRequest>();
        var picker = new FilePreviewPickerProbe { Pick = _ => Task.FromResult<IReadOnlyList<string>>(["new.cs", "NEW.cs", "existing.cs"]) };
        using var pane = Create(picker, calls, execution =>
        {
            Assert.Equal(context.TaskId, execution.TaskId);
            Assert.Equal(context.AuthorizedRoots, execution.AuthorizedRoots);
            Assert.Equal(ApprovalPolicy.ReadOnly, execution.ApprovalPolicy);
            Assert.Equal(TimeSpan.FromSeconds(30), execution.Timeout);
        });
        Assert.False(pane.FilePicker.AddFilesCommand.CanExecute(null));
        pane.Attach(context);
        await pane.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.Equal(2, pane.Tabs.Count);
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal("filesystem.preview_file", call.ToolName));
        Assert.Equal("existing.cs", pane.Active!.Name);
        Assert.Single(picker.Tokens);
        Assert.False(pane.FilePicker.HasStatus);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("policy")]
    [InlineData("hide")]
    [InlineData("dispose")]
    public async Task OwnershipChangeCancelsAndDiscardsLatePickerResults(string change)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new FilePreviewPickerProbe { Pick = _ => pending.Task };
        var calls = new List<ToolRequest>();
        using var pane = Create(picker, calls);
        var context = Context();
        pane.Attach(context);
        await pane.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        var opening = pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.False(pane.FilePicker.CanPick);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.False(Assert.Single(picker.Tokens).IsCancellationRequested);
        switch (change)
        {
            case "scope": pane.Attach(Context()); break;
            case "policy": pane.Attach(context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime }); break;
            case "hide": pane.Hide(); break;
            case "dispose": pane.Dispose(); break;
        }
        Assert.True(picker.Tokens[0].IsCancellationRequested);
        pending.SetResult(["late.cs"]);
        await opening;
        Assert.Single(calls);
        Assert.DoesNotContain(pane.Tabs, tab => tab.Name == "late.cs");
        Assert.False(pane.FilePicker.HasStatus);
    }

    [Fact]
    public async Task CancellationRetainsOwnershipUntilNonCooperativePickerReturns()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new FilePreviewPickerProbe { Pick = _ => pending.Task };
        var calls = new List<ToolRequest>();
        using var pane = Create(picker, calls);
        pane.Attach(Context());
        await pane.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        var opening = pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        pane.FilePicker.AddFilesCommand.Cancel();
        Assert.True(Assert.Single(picker.Tokens).IsCancellationRequested);
        Assert.False(pane.FilePicker.CanPick);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.Single(picker.Tokens);
        pending.SetResult(["cancelled.cs"]);
        await opening;
        Assert.Single(calls);
        Assert.Contains("取消", pane.FilePicker.Status);
        Assert.True(pane.FilePicker.CanPick);
    }

    [Fact]
    public async Task NewTaskCannotReusePendingPickerOrReceiveItsLateSelection()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new FilePreviewPickerProbe { Pick = _ => pending.Task };
        var calls = new List<ToolRequest>();
        using var pane = Create(picker, calls);
        pane.Attach(Context());
        await pane.OpenAsync("first.cs", TestContext.Current.CancellationToken);
        var opening = pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        pane.Attach(Context());
        await pane.OpenAsync("second.cs", TestContext.Current.CancellationToken);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.Single(picker.Tokens);
        Assert.False(pane.FilePicker.CanPick);
        pending.SetResult(["stale.cs"]);
        await opening;
        Assert.Equal("second.cs", Assert.Single(pane.Tabs).Name);
        Assert.Equal(2, calls.Count);
        Assert.False(pane.FilePicker.HasStatus);
        picker.Pick = _ => Task.FromResult<IReadOnlyList<string>>(["current.cs"]);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.Equal("current.cs", pane.Active!.Name);
        Assert.Equal(2, picker.Tokens.Count);
    }

    [Fact]
    public async Task SelectionLimitAndPickerFailureProduceSafeNoticesWithoutReads()
    {
        var picker = new FilePreviewPickerProbe { Pick = _ => Task.FromResult<IReadOnlyList<string>>(Enumerable.Range(0, 9).Select(index => $"{index}.cs").ToArray()) };
        var calls = new List<ToolRequest>();
        using var pane = Create(picker, calls);
        pane.Attach(Context());
        await pane.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.Contains("8", pane.FilePicker.Status);
        picker.Pick = _ => throw new InvalidOperationException("sensitive synthetic exception");
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.True(pane.FilePicker.HasStatus);
        Assert.DoesNotContain("sensitive", pane.FilePicker.Status);
        Assert.Single(calls);
    }

    [Fact]
    public async Task FullPaneAndMissingServiceDisableTheAction()
    {
        var picker = new FilePreviewPickerProbe();
        var calls = new List<ToolRequest>();
        using var pane = Create(picker, calls);
        pane.Attach(Context());
        for (var index = 0; index < FilePreviewPaneViewModel.MaximumTabs; index++)
            await pane.OpenAsync($"{index}.cs", TestContext.Current.CancellationToken);
        Assert.False(pane.FilePicker.CanPick);
        await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
        Assert.Empty(picker.Tokens);
        using var unavailable = Create(null, []);
        unavailable.Attach(Context());
        await unavailable.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        Assert.False(unavailable.FilePicker.AddFilesCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectionAllowsBrowsingBeyondThirtySecondsButHasBoundedTimeout(bool expire)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new FilePreviewPickerProbe { Pick = _ => pending.Task };
        var calls = new List<ToolRequest>();
        using var pane = Create(null, calls);
        pane.Attach(Context());
        await pane.OpenAsync("existing.cs", TestContext.Current.CancellationToken);
        var clock = new FilePreviewPickerClock();
        using var selection = new FilePreviewFilePickerViewModel(pane, picker, clock);
        var opening = selection.AddFilesCommand.ExecuteAsync(null);
        Assert.Equal(TimeSpan.FromMinutes(5), clock.LastDueTime);
        clock.Advance(TimeSpan.FromSeconds(45));
        Assert.False(Assert.Single(picker.Tokens).IsCancellationRequested);
        if (expire) clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(expire, picker.Tokens[0].IsCancellationRequested);
        Assert.False(selection.CanPick);
        await selection.AddFilesCommand.ExecuteAsync(null);
        Assert.Single(picker.Tokens);
        pending.SetResult(["selected.cs"]);
        await opening;
        Assert.Equal(expire ? 1 : 2, calls.Count);
        Assert.Equal(expire ? "existing.cs" : "selected.cs", pane.Active!.Name);
        Assert.Equal(expire, selection.HasStatus);
        Assert.True(selection.CanPick);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(selection.CanPick);
    }

    private static ToolExecutionContext Context() => new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);

    private static FilePreviewPaneViewModel Create(FilePreviewPickerProbe? picker, List<ToolRequest> calls, Action<ToolExecutionContext>? inspect = null) =>
        new(new InspectorProbeToolExecutor
        {
            Execute = (request, context, _) =>
            {
                inspect?.Invoke(context);
                calls.Add(request);
                return Task.FromResult(new ToolResult(true, "preview", Artifacts: [new("file", "snapshot", "text/plain", 0, "hash")]));
            }
        }, new PreviewProbeArtifactReader(), picker);
}
