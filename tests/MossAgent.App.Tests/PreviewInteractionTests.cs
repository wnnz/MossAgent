using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class PreviewInteractionTests
{
    [Fact]
    public void LiteralSearch_IsCaseInsensitiveLineBasedAndWrapsBothDirections()
    {
        using var tab = new FilePreviewTabViewModel("file.cs", "file.cs", "file.cs")
        { Text = "HELLO.* hello.*\r\nno\r\nhello.*", IsReady = true };
        var search = tab.Search;
        search.Query = "hello.*";
        search.ShowCommand.Execute(null);
        Assert.Equal(2, search.MatchCount);
        Assert.Equal(0, search.SelectedLine);
        search.PreviousCommand.Execute(null);
        Assert.Equal(2, search.SelectedLine);
        search.NextCommand.Execute(null);
        Assert.Equal(0, search.SelectedLine);
        search.NextCommand.Execute(null);
        Assert.Equal(2, search.SelectedLine);
        search.CloseCommand.Execute(null);
        Assert.Equal(-1, search.SelectedLine);
        Assert.Empty(search.ActiveQuery);
        search.ShowCommand.Execute(null);
        Assert.Equal(2, search.SelectedLine);
    }

    [Fact]
    public void EmptyNoMatchAndOversizedSearch_StayBoundedToRenderedContent()
    {
        using var tab = new FilePreviewTabViewModel("file", "file", "file")
        { Text = string.Concat(Enumerable.Repeat("line\n", 2000)) + "hidden", IsReady = true };
        var search = tab.Search;
        Assert.False(search.NextCommand.CanExecute(null));
        search.ShowCommand.Execute(null);
        search.Query = "hidden";
        Assert.Equal(0, search.MatchCount);
        Assert.Contains("没有", search.ResultLabel);
        search.NextCommand.Execute(null);
        Assert.Equal(-1, search.SelectedLine);
        search.Query = new string('x', 1000);
        Assert.Equal(FilePreviewSearchViewModel.MaximumQueryLength, search.Query.Length);
        tab.Text = new string('x', CodePreviewParser.MaximumCharacters) + "beyond";
        search.ShowCommand.Execute(null);
        search.Query = "beyond";
        Assert.Equal(0, search.MatchCount);
        tab.IsReady = false;
        Assert.False(search.IsOpen);
        Assert.False(search.ShowCommand.CanExecute(null));
        search.ShowCommand.Execute(null);
        Assert.False(search.IsOpen);
        tab.Dispose();
        Assert.False(search.NextCommand.CanExecute(null));
        Assert.False(search.ShowCommand.CanExecute(null));
    }

    [Fact]
    public async Task TabsRetainTheirOwnSearch_RefreshAndScopeChangeResetIt()
    {
        var calls = 0;
        using var pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor
        { Execute = (_, _, _) => { calls++; return Task.FromResult(new ToolResult(true, "ok", Artifacts: [new("file", "snapshot", "text/plain", 0, "hash")])); } },
            new PreviewProbeArtifactReader { Read = (_, _, _) => Task.FromResult(Encoding.UTF8.GetBytes("match\nmatch")) });
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.ReadOnly);
        pane.Attach(context);
        await pane.OpenAsync("a.cs", TestContext.Current.CancellationToken);
        var first = pane.Active!;
        first.Search.ShowCommand.Execute(null);
        first.Search.Query = "match";
        first.Search.NextCommand.Execute(null);
        await pane.OpenAsync("b.cs", TestContext.Current.CancellationToken);
        Assert.False(pane.Active!.Search.IsOpen);
        pane.SelectCommand.Execute(first);
        Assert.True(first.Search.IsOpen);
        Assert.Equal(1, first.Search.SelectedLine);
        Assert.Equal(2, calls);
        await pane.RefreshCommand.ExecuteAsync(null);
        Assert.False(first.Search.IsOpen);
        Assert.Empty(first.Search.Query);
        Assert.Equal(3, calls);
        pane.Attach(context with { TaskId = Guid.NewGuid() });
        Assert.False(first.Search.ShowCommand.CanExecute(null));
        Assert.Empty(pane.Tabs);
    }
}
