using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class PreviewProjectionAllocationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parsing_DoesNotMaterializeLinesBeyondTheVisibleLimit(bool diff)
    {
        var content = CreateLongContent();
        for (var iteration = 0; iteration < 10; iteration++) CodePreviewParser.Parse(content, "a.cs", diff);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var rows = CodePreviewParser.Parse(content, "a.cs", diff);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Preview parse (diff={diff}): {allocated} bytes");
        Assert.Equal(CodePreviewParser.MaximumLines + 1, rows.Count);
        Assert.True(rows[^1].IsHeader);
        Assert.InRange(allocated, 0, 600_000);
    }

    [Fact]
    public void SearchReset_DoesNotMaterializeHiddenLines()
    {
        var content = CreateLongContent();
        var search = new FilePreviewSearchViewModel();
        for (var iteration = 0; iteration < 10; iteration++) search.Reset(content);
        var before = GC.GetAllocatedBytesForCurrentThread();

        search.Reset(content);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Preview search reset: {allocated} bytes");
        search.Query = "hidden";
        Assert.Equal(0, search.MatchCount);
        Assert.InRange(allocated, 0, 200_000);
    }

    [Fact]
    public void NoMatchSearch_AvoidsFilteringIteratorsAndTemporaryLists()
    {
        var search = new FilePreviewSearchViewModel();
        search.Reset(CreateLongContent());
        for (var iteration = 0; iteration < 100; iteration++) search.Query = iteration % 2 == 0 ? "absent" : "missing";
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++) search.Query = iteration % 2 == 0 ? "absent" : "missing";

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Preview no-match searches (1,000): {allocated} bytes");
        Assert.Equal(0, search.MatchCount);
        Assert.InRange(allocated, 0, 192_000);
    }

    private static string CreateLongContent() =>
        string.Concat(Enumerable.Repeat("shown\n", CodePreviewParser.MaximumLines)) + new string('\n', 100_000) + "hidden";
}
