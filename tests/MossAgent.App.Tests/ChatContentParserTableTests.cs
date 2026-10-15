using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ChatContentParserTableTests
{
    [Theory]
    [InlineData("普通第一行\n普通第二行")]
    [InlineData("Name\n---")]
    [InlineData("Name|Value")]
    [InlineData("Name|Value\n---")]
    [InlineData("| Name | Value |\n| -- | --- |")]
    [InlineData("| Name | Value |\n| --- | invalid |")]
    public void InvalidTable_RemainsAnUnchangedParagraph(string content)
    {
        var paragraph = Assert.IsType<ChatTextBlockViewModel>(Assert.Single(ChatContentParser.Parse(content)));

        Assert.Equal(content.Replace("\n", Environment.NewLine), paragraph.Text);
    }

    [Theory]
    [InlineData("| Name |\n| --- |\n| value |", "Name")]
    [InlineData("Name\n| --- |\n| value |", "Name")]
    [InlineData("| Name |\n---\n| value |", "Name")]
    [InlineData("| |\n| --- |\n| value |", "")]
    public void SingleColumnTable_AcceptsAPipeOnEitherHeaderOrSeparator(string content, string header)
    {
        var table = Assert.IsType<ChatTableBlockViewModel>(Assert.Single(ChatContentParser.Parse(content)));

        Assert.Equal(header, Assert.Single(table.Headers));
        Assert.Equal("value", Assert.Single(Assert.Single(table.Rows)));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Table_KeepsEscapedAndCodePipesAndPadsShortRows(string newline)
    {
        var content = string.Join(newline,
            "before", "", "| a\\|b | `c|d` |", "| :--- | ---: |", "| first |", "| second | third | ignored |", "", "after");
        var blocks = ChatContentParser.Parse(content);

        Assert.Equal("before", Assert.IsType<ChatTextBlockViewModel>(blocks[0]).Text);
        var table = Assert.IsType<ChatTableBlockViewModel>(blocks[1]);
        Assert.Equal(new[] { "a|b", "`c|d`" }, table.Headers);
        Assert.Equal(new[] { "first", "" }, table.Rows[0]);
        Assert.Equal(new[] { "second", "third" }, table.Rows[1]);
        Assert.Equal("after", Assert.IsType<ChatTextBlockViewModel>(blocks[2]).Text);
        Assert.Equal(3, blocks.Count);
    }

    [Theory]
    [InlineData("```text\nName|Value\n---|---\n```", "text")]
    [InlineData("```markdown\n| Name | Value |\n| --- | --- |", "markdown")]
    public void CodeFence_DoesNotParseItsContentsAsATable(string content, string language)
    {
        var code = Assert.IsType<ChatCodeBlockViewModel>(Assert.Single(ChatContentParser.Parse(content)));

        Assert.Equal(language, code.Language);
        Assert.Contains("Name", code.Code, StringComparison.Ordinal);
        Assert.Contains("Value", code.Code, StringComparison.Ordinal);
    }
}
