using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ResponseContentFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "Light", 280)]
    [InlineData(false, "Light", 740)]
    [InlineData(false, "Dark", 280)]
    [InlineData(false, "Dark", 740)]
    [InlineData(true, "Light", 280)]
    [InlineData(true, "Light", 740)]
    [InlineData(true, "Dark", 280)]
    [InlineData(true, "Dark", 740)]
    public Task ParagraphsAndListsKeepSharedTypographyAndSemanticSpacing(bool compact, string theme, double width) =>
        _session.Dispatch(() =>
        {
            var view = new AppResponseContent { Blocks = ChatContentParser.Parse("段落\n\n- 第一项 `path`\n- 第二项\n\n结束") };
            if (compact) view.Classes.Add("compact");
            var window = new Window
            {
                Width = width,
                Height = 420,
                Content = view,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var texts = view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>()
                    .Where(text => text.IsEffectivelyVisible).OrderBy(text => text.TranslatePoint(default, view)!.Value.Y).ToArray();
                Assert.Equal(4, texts.Length);
                var fontSize = compact ? 12.5 : 14;
                var lineHeight = compact ? 19 : 23;
                foreach (var text in texts) { Assert.Equal(fontSize, text.FontSize); Assert.Equal(lineHeight, text.LineHeight); }
                var origins = texts.Select(text => text.TranslatePoint(default, view)!.Value).ToArray();
                Assert.Equal(lineHeight + 2, origins[1].Y - origins[0].Y);
                Assert.Equal(lineHeight, origins[2].Y - origins[1].Y);
                Assert.Equal(lineHeight + 8, origins[3].Y - origins[2].Y);
                Assert.Equal(28, origins[1].X - origins[0].X);
                var prefixes = view.GetVisualDescendants().OfType<AppText>()
                    .Where(text => text.Classes.Contains("response-list-prefix") && text.IsEffectivelyVisible).ToArray();
                Assert.Equal(2, prefixes.Length);
                foreach (var prefix in prefixes) { Assert.Equal(fontSize, prefix.FontSize); Assert.Equal(lineHeight, prefix.LineHeight); }
                var code = Assert.Single(texts[1].Inlines!.OfType<Run>(), run => run.Text == "path");
                Assert.Equal(compact ? 10.5 : 12, code.FontSize);
                Assert.NotNull(texts[1].CodeBackground);
                Assert.Null(code.Background);
                Assert.InRange(origins[1].X + texts[1].Bounds.Width, 1, view.Bounds.Width);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task SmallerInlineCodeKeepsTheReferencePathOnOneLine(string theme) =>
        _session.Dispatch(() =>
        {
            const string markdown = "未改界面或发布配置，未提交。详情见 `D:\\Dev\\MossAgent\\docs\\GEMINI_CONTINUATION_ACCEPTANCE.md:70`。";
            var view = new AppResponseContent { Blocks = ChatContentParser.Parse(markdown) };
            var window = new Window
            {
                Width = 732,
                Height = 160,
                Content = view,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var text = Assert.Single(view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(), text => text.IsEffectivelyVisible);
                Assert.Equal(23, text.Bounds.Height);
                text.SelectionStart = 0;
                text.SelectionEnd = markdown.Replace("`", "").Length;
                Assert.Equal(markdown.Replace("`", ""), text.SelectedText);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task LiveCodeStylesRetainSelectionAndReleaseReplacedRuns(string theme) =>
        _session.Dispatch(() =>
        {
            var view = new AppResponseContent { Blocks = ChatContentParser.Parse("前 **`path`** 后") };
            var window = new Window
            {
                Width = 420,
                Height = 160,
                Content = view,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var text = Assert.Single(view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(), text => text.IsEffectivelyVisible);
                var code = Assert.Single(Assert.Single(text.Inlines!.OfType<Span>()).Inlines.OfType<Run>());
                text.SelectionStart = 2;
                text.SelectionEnd = 6;
                var initialBackground = text.CodeBackground!.ToString();
                window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Light : ThemeVariant.Dark;
                window.UpdateLayout();
                Assert.NotEqual(initialBackground, text.CodeBackground!.ToString());
                text.CodeFontSize = 11;
                text.CodeBackground = Brushes.Gray;
                text.CodeFontFamily = new FontFamily("Consolas");
                window.UpdateLayout();
                Assert.Equal("path", text.SelectedText);
                Assert.Equal(11, code.FontSize);
                Assert.Same(Brushes.Gray, text.CodeBackground);
                Assert.Null(code.Background);
                Assert.Equal("Consolas", code.FontFamily.Name);
                text.Markdown = "新 `value`";
                var replacement = Assert.Single(text.Inlines!.OfType<Run>(), run => run.Text == "value");
                Assert.NotSame(code, replacement);
                text.CodeFontSize = 13;
                Assert.Equal(13, replacement.FontSize);
                Assert.Equal(11, code.FontSize);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
