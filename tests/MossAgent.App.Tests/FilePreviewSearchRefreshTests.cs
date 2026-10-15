using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewSearchRefreshTests
{
    [Theory]
    [InlineData("")]
    [InlineData("match")]
    public void Reset_PublishesOneResultRefresh(string query)
    {
        var search = new FilePreviewSearchViewModel();
        search.Reset("match\nother");
        search.Query = query;
        var changed = new List<string?>();
        search.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        search.Reset("replacement");

        Assert.Equal(1, changed.Count(property => property == nameof(search.MatchCount)));
        Assert.Equal(1, changed.Count(property => property == nameof(search.SelectedLine)));
        Assert.Equal(1, changed.Count(property => property == nameof(search.ActiveQuery)));
        Assert.Equal(1, changed.Count(property => property == nameof(search.ResultLabel)));
        Assert.Equal(query.Length == 0 ? 0 : 1, changed.Count(property => property == nameof(search.Query)));
        Assert.Empty(search.Query);
        Assert.Equal(0, search.MatchCount);
        Assert.Equal(-1, search.SelectedLine);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r\r\n")]
    public void MatchingLineIndices_AgreeWithRenderedRows(string newline)
    {
        const string query = "target.*";
        var content = $"TARGET.* twice target.*{newline}none{newline}target.*{newline}";
        var rows = CodePreviewParser.Parse(content, "a.cs", false);
        using var tab = new FilePreviewTabViewModel("a.cs", "a.cs", "a.cs") { Text = content, IsReady = true };
        tab.Search.Query = query;
        tab.Search.ShowCommand.Execute(null);

        Assert.Equal(2, tab.Search.MatchCount);
        Assert.Equal(0, tab.Search.SelectedLine);
        Assert.Contains(query, rows[tab.Search.SelectedLine].Code, StringComparison.OrdinalIgnoreCase);
        tab.Search.NextCommand.Execute(null);
        Assert.Equal(2, tab.Search.SelectedLine);
        Assert.Contains(query, rows[tab.Search.SelectedLine].Code, StringComparison.OrdinalIgnoreCase);
        tab.Search.NextCommand.Execute(null);
        Assert.Equal(0, tab.Search.SelectedLine);
    }

    [Fact]
    public void ContentReplacement_ResetsOnlyItsOwnSearch()
    {
        using var first = new FilePreviewTabViewModel("a", "a", "a") { Text = "match\nmatch", IsReady = true };
        using var second = new FilePreviewTabViewModel("b", "b", "b") { Text = "match\nmatch", IsReady = true };
        foreach (var tab in new[] { first, second })
        {
            tab.Search.ShowCommand.Execute(null);
            tab.Search.Query = "match";
            tab.Search.NextCommand.Execute(null);
        }

        first.Text = "replacement";

        Assert.False(first.Search.IsOpen);
        Assert.Empty(first.Search.Query);
        Assert.Equal(0, first.Search.MatchCount);
        Assert.True(second.Search.IsOpen);
        Assert.Equal("match", second.Search.Query);
        Assert.Equal(2, second.Search.MatchCount);
        Assert.Equal(1, second.Search.SelectedLine);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CharacterClippedNewline_RemainsConsistentWithThePreview(int remaining)
    {
        var content = new string('x', CodePreviewParser.MaximumCharacters - remaining) + "\r\nhidden";
        using var tab = new FilePreviewTabViewModel("a", "a", "a") { Text = content, IsReady = true };
        tab.Search.ShowCommand.Execute(null);
        tab.Search.Query = "\r";

        Assert.Equal(remaining == 1 ? 1 : 0, tab.Search.MatchCount);
        tab.Search.Query = "hidden";
        Assert.Equal(0, tab.Search.MatchCount);
    }
}
