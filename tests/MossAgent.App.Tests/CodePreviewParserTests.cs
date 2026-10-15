using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class CodePreviewParserTests
{
    [Fact]
    public void Diff_HasCorrectOldNewGuttersPrefixesAndSyntaxLanguage()
    {
        var rows = CodePreviewParser.Parse("@@ -10,2 +20,2 @@\n context\n-const old = 1;\n+const newer = 2;", "sample.ts", true);
        Assert.True(rows[0].IsHeader);
        Assert.Equal(("10", "20"), (rows[1].OldNumber, rows[1].NewNumber));
        Assert.Equal(("11", ""), (rows[2].OldNumber, rows[2].NewNumber));
        Assert.True(rows[2].IsRemoved);
        Assert.Equal(("", "21"), (rows[3].OldNumber, rows[3].NewNumber));
        Assert.True(rows[3].IsAdded);
        Assert.Equal("typescript", rows[3].Language);
        Assert.Equal("const newer = 2;", rows[3].Code);
    }

    [Fact]
    public void Text_NormalizesCrLfAndDoesNotInterpretMarkup()
    {
        var rows = CodePreviewParser.Parse("<script>\r\nhello", "sample.cs", false);
        Assert.Equal("<script>", rows[0].Code);
        Assert.Equal("2", rows[1].NewNumber);
        Assert.Equal("csharp", rows[1].Language);
    }

    [Fact]
    public void Diff_OmitsGitEnvelopeAndResetsAtEachNewFile()
    {
        var rows = CodePreviewParser.Parse("warning: CRLF\ndiff --git a/a.cs b/a.cs\nindex abc..def\n--- a/a.cs\n+++ b/a.cs\n@@ -1 +1 @@\n-before\n+after\ndiff --git a/b.cs b/b.cs\n--- a/b.cs\n+++ b/b.cs\n@@ -5 +5 @@\n text", "a.cs", true);
        Assert.Equal(5, rows.Count);
        Assert.True(rows[0].IsHeader);
        Assert.True(rows[3].IsHeader);
        Assert.Equal(("5", "5"), (rows[4].OldNumber, rows[4].NewNumber));
    }

    [Fact]
    public void MalformedAndOversizedDiff_IsBoundedAndDoesNotThrow()
    {
        var rows = CodePreviewParser.Parse("@@ -99999999999999999999 +1 @@\n" + string.Concat(Enumerable.Repeat("+line\n", 3000)), "file.cs", true);
        Assert.Equal(CodePreviewParser.MaximumLines + 1, rows.Count);
        Assert.True(rows[^1].IsHeader);
    }
}
