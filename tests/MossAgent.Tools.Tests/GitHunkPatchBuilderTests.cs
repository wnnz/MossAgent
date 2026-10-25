using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitHunkPatchBuilderTests
{
    private const string TwoHunkDiff = """
        diff --git a/f.txt b/f.txt
        index 4f11e05..980abbb 100644
        --- a/f.txt
        +++ b/f.txt
        @@ -1,5 +1,5 @@
         a1
        -a2
        +CHANGED
         a3
         a4
         a5
        @@ -7,4 +7,5 @@ a6
         a7
         a8
         a9
        -a10
        +B10
        +B11
        """;

    private const string ThreeHunkDiff = """
        diff --git a/f.txt b/f.txt
        index 1111111..2222222 100644
        --- a/f.txt
        +++ b/f.txt
        @@ -1,3 +1,3 @@
         l1
        -l2
        +x2
        @@ -8,3 +8,3 @@
         l8
        -l9
        +x9
        @@ -15,3 +15,4 @@
         l15
        -l16
        +x16
        +x17
        """;

    [Fact]
    public void SelectedHunksKeepHeaderAndOriginalOrder()
    {
        var result = GitHunkPatchBuilder.Build(ThreeHunkDiff, [2, 0]);
        Assert.Equal(2, result.SelectedHunks);
        Assert.Equal(3, result.TotalHunks);
        Assert.StartsWith("diff --git a/f.txt b/f.txt\nindex 1111111..2222222 100644\n--- a/f.txt\n+++ b/f.txt\n", result.Patch);
        var first = result.Patch.IndexOf("@@ -1,3", StringComparison.Ordinal);
        var second = result.Patch.IndexOf("@@ -15,3", StringComparison.Ordinal);
        Assert.True(first > 0 && second > first);
        Assert.DoesNotContain("@@ -8,3", result.Patch);
        Assert.EndsWith("+x17\n", result.Patch);
    }

    [Fact]
    public void DuplicateOrdinalsAreSelectedOnce()
    {
        var result = GitHunkPatchBuilder.Build(TwoHunkDiff, [1, 1, 1]);
        Assert.Equal(1, result.SelectedHunks);
        Assert.Contains("@@ -7,4", result.Patch);
        Assert.DoesNotContain("@@ -1,5", result.Patch);
    }

    [Fact]
    public void NoNewlineMarkersStayAttachedToTheirHunk()
    {
        const string diff = """
            diff --git a/f.txt b/f.txt
            index aaaaaaa..bbbbbbb 100644
            --- a/f.txt
            +++ b/f.txt
            @@ -1,3 +1,3 @@
             l1
            -l2
            +x2
            \ No newline at end of file
            @@ -8,2 +8,2 @@
            -l9
            +x9
            """;
        var last = GitHunkPatchBuilder.Build(diff, [1]);
        Assert.Contains("-l9\n+x9\n", last.Patch);
        Assert.DoesNotContain("No newline", last.Patch);
        var first = GitHunkPatchBuilder.Build(diff, [0]);
        Assert.Contains("+x2\n\\ No newline at end of file\n", first.Patch);
    }

    [Fact]
    public void ModeChangeHeadersAreStrippedWhileHunksRemainSelectable()
    {
        const string diff = """
            diff --git a/s.sh b/s.sh
            old mode 100755
            new mode 100644
            index ae2dfb4..7d6f384
            --- a/s.sh
            +++ b/s.sh
            @@ -1,5 +1,5 @@
             l1
            -l2
            +CHANGED
             l3
             l4
             l5
            """;
        var result = GitHunkPatchBuilder.Build(diff, [0]);
        Assert.DoesNotContain("mode", result.Patch);
        Assert.Contains("@@ -1,5 +1,5 @@", result.Patch);
    }

    [Theory]
    [InlineData("diff --git a/f.bin b/f.bin\nindex 1..2 100644\nBinary files a/f.bin and b/f.bin differ\n")]
    [InlineData("diff --git a/f.bin b/f.bin\nindex 1..2 100644\nGIT binary patch\nliteral 4\ndeadbeef\n")]
    public void BinaryDiffsAreRefused(string diff) =>
        Assert.Equal("git_hunks_unsupported", Refusal(diff).Code);

    [Theory]
    [InlineData("similarity index 100%\nrename from a.txt\nrename to b.txt\n")]
    [InlineData("similarity index 88%\ncopy from a.txt\ncopy to b.txt\nindex 1..2 100644\n")]
    public void RenameAndCopyDiffsAreRefused(string headers) =>
        Assert.Equal("git_hunks_unsupported", Refusal("diff --git a/a.txt b/b.txt\n" + headers).Code);

    [Theory]
    [InlineData("new file mode 100644\nindex 0000000..1234567\n--- /dev/null\n+++ b/new.txt\n@@ -0,0 +1 @@\n+content\n")]
    [InlineData("deleted file mode 100644\nindex 1234567..0000000\n--- a/old.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-content\n")]
    public void WholeFileAddOrDeleteDiffsAreRefused(string body) =>
        Assert.Equal("git_hunks_unsupported", Refusal("diff --git a/x b/x\n" + body).Code);

    [Fact]
    public void ModeOnlyDiffIsRefused() =>
        Assert.Equal("git_hunks_unsupported",
            Refusal("diff --git a/s.sh b/s.sh\nold mode 100644\nnew mode 100755\n").Code);

    [Theory]
    [InlineData("* Unmerged path f.txt\n")]
    [InlineData("diff --cc f.txt\nindex 1,2..0\n--- a/f.txt\n+++ b/f.txt\n@@@ -1,3 -1,3 +1,7 @@@\n  one\n")]
    public void ConflictDiffsAreRefused(string diff) =>
        Assert.Equal("git_hunks_unsupported", Refusal(diff).Code);

    [Theory]
    [InlineData("complete garbage\n")]
    [InlineData("diff --git a/f.txt b/f.txt\n@@ -1 +1 @@\n a\nunexpected line\n")]
    [InlineData("diff --git a/f.txt b/f.txt\nindex 1..2 100644\n--- a/f.txt\n+++ b/f.txt\n@@ -1 +1 @@\n-a\n+b\ndiff --git a/g.txt b/g.txt\n@@ -1 +1 @@\n-c\n+d\n")]
    public void MalformedDiffsAreRefused(string diff) =>
        Assert.Equal("git_hunks_unsupported", Refusal(diff).Code);

    [Fact]
    public void OutOfRangeOrdinalListsTheValidRange()
    {
        var failure = Assert.Throws<GitHunksException>(() => GitHunkPatchBuilder.Build(TwoHunkDiff, [0, 2]));
        Assert.Equal("git_hunks_out_of_range", failure.Code);
    }

    [Fact]
    public void ZeroHunkDiffHasNothingSelectable()
    {
        var failure = Assert.Throws<GitHunksException>(() =>
            GitHunkPatchBuilder.Build("diff --git a/f b/f\nindex 1..1 100644\n", [0]));
        Assert.Equal("git_hunks_none", failure.Code);
    }

    private static GitHunksException Refusal(string diff) =>
        Assert.Throws<GitHunksException>(() => GitHunkPatchBuilder.Build(diff, [0]));
}
