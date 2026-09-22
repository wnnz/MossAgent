using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class GitStatusParserTests
{
    [Fact]
    public void Parse_ReadsNullTerminatedStatusesAndRenameDestination()
    {
        var content = "## main\0 M file one.txt\0R  new.txt\0old.txt\0?? 新文件.txt\0";

        var files = GitStatusParser.Parse(content);

        Assert.Collection(
            files,
            file =>
            {
                Assert.Equal("file one.txt", file.Path);
                Assert.False(file.HasStagedChange);
                Assert.True(file.HasUnstagedChange);
            },
            file =>
            {
                Assert.Equal("new.txt", file.Path);
                Assert.True(file.HasStagedChange);
                Assert.False(file.HasUnstagedChange);
            },
            file =>
            {
                Assert.Equal("新文件.txt", file.Path);
                Assert.Equal("未跟踪", file.StatusLabel);
            });
    }

    [Fact]
    public void Parse_ReadsLineBasedRenameFallback()
    {
        var files = GitStatusParser.Parse("R  old.txt -> new.txt\nMM changed.cs\n");

        Assert.Equal("new.txt", files[1].Path);
        Assert.Equal("changed.cs", files[0].Path);
        Assert.True(files[0].HasStagedChange);
        Assert.True(files[0].HasUnstagedChange);
    }
}
