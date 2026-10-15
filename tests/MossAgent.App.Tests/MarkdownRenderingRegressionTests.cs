using Avalonia.Controls;
using Avalonia.Headless;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class MarkdownRenderingRegressionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(null, "")]
    [InlineData("```text\ncode\n```", "```text\ncode\n```")]
    [InlineData("first\n\nsecond", "first\nsecond")]
    [InlineData("first\n\n# ignored\n\nsecond", "first\nsecond")]
    [InlineData("**[label `code`](https://example.com)**", "label code")]
    public Task ParagraphEnumerationPreservesSelectableContent(string? markdown, string expected) =>
        _session.Dispatch(() =>
        {
            var text = new MarkdownSelectableTextBlock { Markdown = markdown };
            var window = new Window { Width = 480, Height = 180, Content = text };
            window.Show();
            try
            {
                window.UpdateLayout();
                text.SelectAll();
                Assert.Equal(expected, text.SelectedText?.ReplaceLineEndings("\n") ?? string.Empty);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
