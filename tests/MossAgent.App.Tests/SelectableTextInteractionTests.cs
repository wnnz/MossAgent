using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SelectableTextInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("Plain", "Light")]
    [InlineData("Markdown", "Light")]
    [InlineData("Syntax", "Light")]
    [InlineData("Plain", "Dark")]
    [InlineData("Markdown", "Dark")]
    [InlineData("Syntax", "Dark")]
    public Task SharedOutputsKeepNativeSelectionAppearance(string kind, string theme) =>
        _session.Dispatch(() =>
        {
            var text = CreateText(kind);
            var native = new SelectableTextBlock { Text = "原生选择参考" };
            var window = new Window
            {
                Width = 480,
                Height = 240,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new StackPanel { Children = { native, text } }
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.IsAssignableFrom<ISolidColorBrush>(native.SelectionBrush);
                Assert.Equal(native.SelectionBrush!.ToString(), text.SelectionBrush?.ToString());
                Assert.Equal(native.SelectionForegroundBrush?.ToString(), text.SelectionForegroundBrush?.ToString());
                text.SelectAll();
                Assert.Equal("可选择的正文 path", text.SelectedText);
                var selected = text.SelectedText;
                window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Light : ThemeVariant.Dark;
                window.UpdateLayout();
                Assert.Equal(selected, text.SelectedText);
                Assert.Equal(native.SelectionBrush!.ToString(), text.SelectionBrush?.ToString());
                Assert.Equal(native.SelectionForegroundBrush?.ToString(), text.SelectionForegroundBrush?.ToString());
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static AppSelectableText CreateText(string kind) => kind switch
    {
        "Markdown" => new MarkdownSelectableTextBlock { Markdown = "**可选择的正文** `path`" },
        "Syntax" => new SyntaxHighlightedSelectableTextBlock { Code = "可选择的正文 path", Language = "text" },
        _ => new AppSelectableText { Text = "可选择的正文 path" }
    };
}
