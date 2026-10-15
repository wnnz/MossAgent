using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class CodePreviewBoundaryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("one")]
    [InlineData("one\n")]
    [InlineData("\n\n")]
    [InlineData("one\r\ntwo\r\n")]
    [InlineData("one\rtwo\r")]
    [InlineData("one\r\r\ntwo")]
    [InlineData("one\r\n\ntwo\nthree\r\n")]
    public void PlainText_PreservesEmptyLinesAndOnlyNormalizesCrLf(string content) =>
        AssertReferenceProjection(content);

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(1999, true)]
    [InlineData(2000, false)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    [InlineData(2001, true)]
    [InlineData(3000, true)]
    public void LineLimit_RetainsExactBoundaryAndTrailingEmptyLine(int count, bool trailingNewline)
    {
        var content = string.Join('\n', Enumerable.Repeat("line", count)) + (trailingNewline ? "\n" : "");
        AssertReferenceProjection(content);
    }

    [Theory]
    [InlineData(1, "\r\nhidden")]
    [InlineData(2, "\r\nhidden")]
    [InlineData(1, "\nhidden")]
    [InlineData(2, "\r\r\nhidden")]
    [InlineData(0, "")]
    public void CharacterLimit_AppliesBeforeCrLfNormalization(int remaining, string suffix) =>
        AssertReferenceProjection(new string('x', CodePreviewParser.MaximumCharacters - remaining) + suffix);

    [Fact]
    public void Diff_RetainsHunkNumbersAndLoneCarriageReturns()
    {
        var rows = CodePreviewParser.Parse("diff --git a/a.cs b/a.cs\r\n@@ -5 +6 @@\r\n before\r\r\n+after\r\n", "a.cs", true);

        Assert.Equal(3, rows.Count);
        Assert.True(rows[0].IsHeader);
        Assert.Equal(("5", "6", "before\r"), (rows[1].OldNumber, rows[1].NewNumber, rows[1].Code));
        Assert.Equal(("", "7", "after"), (rows[2].OldNumber, rows[2].NewNumber, rows[2].Code));
        Assert.True(rows[2].IsAdded);
    }

    private static void AssertReferenceProjection(string content)
    {
        var expected = content[..Math.Min(content.Length, CodePreviewParser.MaximumCharacters)]
            .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var rows = CodePreviewParser.Parse(content, "a.cs", false);
        var count = Math.Min(expected.Length, CodePreviewParser.MaximumLines);
        var truncated = expected.Length > CodePreviewParser.MaximumLines || content.Length > CodePreviewParser.MaximumCharacters;

        Assert.Equal(count + (truncated ? 1 : 0), rows.Count);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(expected[index], rows[index].Code);
            Assert.Equal((index + 1).ToString(), rows[index].NewNumber);
            Assert.Equal("csharp", rows[index].Language);
            Assert.False(rows[index].IsHeader);
        }
        if (truncated)
        {
            Assert.True(rows[^1].IsHeader);
            Assert.Equal(InspectorText.Get("PreviewTruncated"), rows[^1].Code);
        }
    }
}
