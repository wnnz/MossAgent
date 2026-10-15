using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CodePreviewGutterTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, 300)]
    [InlineData(true, false, 300)]
    [InlineData(false, true, 300)]
    [InlineData(true, true, 300)]
    [InlineData(false, false, 640)]
    [InlineData(true, false, 640)]
    [InlineData(false, true, 640)]
    [InlineData(true, true, 640)]
    public Task HorizontalScroll_KeepsNumbersAndMarkersFixedWhileCodeMoves(bool dark, bool diff, int width) => _session.DispatchAsync(() =>
    {
        var longLine = "public string Value => \"" + new string('x', 180) + "\";";
        var preview = new AppCodePreview { Code = diff ? "@@ -1 +1 @@\n-old\n+" + longLine : longLine, FilePath = "test.cs", IsDiff = diff };
        var window = new AppWindow { Content = preview, Width = width, Height = 260, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var text = preview.GetVisualDescendants().OfType<SyntaxHighlightedSelectableTextBlock>().Last();
            var numbers = preview.GetVisualDescendants().OfType<AppText>().Where(control => control.Classes.Contains("file-code-number")
                && control.IsEffectivelyVisible && !string.IsNullOrEmpty(control.Text)).ToArray();
            var positions = numbers.Select(control => control.TranslatePoint(default, preview)!.Value).ToArray();
            var codePosition = text.TranslatePoint(default, preview)!.Value;
            var scroller = preview.FindControl<AppScrollViewer>("Scroller")!;
            scroller.Offset = new Vector(150, 0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(150, scroller.Offset.X);
            Assert.All(numbers.Select((control, index) => (control, index)), pair =>
                Assert.InRange(Math.Abs(pair.control.TranslatePoint(default, preview)!.Value.X - positions[pair.index].X), 0, .01));
            Assert.InRange(Math.Abs(text.TranslatePoint(default, preview)!.Value.X - codePosition.X + 150), 0, .01);
            Assert.All(numbers, control => Assert.InRange(control.TranslatePoint(default, preview)!.Value.X, 0, 104));
            if (diff) Assert.All(preview.GetVisualDescendants().OfType<AppText>().Where(control => control.Classes.Contains("file-code-prefix")),
                control => Assert.InRange(control.TranslatePoint(default, preview)!.Value.X, 0, 104));
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
