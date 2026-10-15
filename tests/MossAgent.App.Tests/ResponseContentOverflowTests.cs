using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ResponseContentOverflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, "Light")]
    [InlineData(740, "Light")]
    [InlineData(280, "Dark")]
    [InlineData(740, "Dark")]
    public Task WideCodeAndTables_ScrollInsideBoundedResponse(double width, string theme) =>
        _session.Dispatch(() =>
        {
            var line = "var longValue = \"" + new string('x', 400) + "\";";
            var code = string.Join('\n', Enumerable.Repeat(line, 40));
            var view = new AppResponseContent
            {
                Blocks = ChatContentParser.Parse("```csharp\n" + code + "\n```\n\n"
                    + "| One | Two | Three | Four | Five |\n| --- | --- | --- | --- | --- |\n| 1 | 2 | 3 | 4 | 5 |")
            };
            var outer = new AppScrollViewer { Content = view };
            var window = new Window
            {
                Width = width,
                Height = 600,
                Content = outer,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.InRange(outer.Extent.Width, 0, outer.Viewport.Width + 1);
                var codeScroll = Assert.Single(view.GetVisualDescendants().OfType<AppScrollViewer>(),
                    viewer => viewer.Classes.Contains("response-code-scroll"));
                var tableScroll = Assert.Single(view.GetVisualDescendants().OfType<AppResponseTable>())
                    .GetVisualDescendants().OfType<AppScrollViewer>().Single();
                foreach (var viewer in new[] { codeScroll, tableScroll })
                {
                    Assert.True(viewer.Extent.Width > viewer.Viewport.Width);
                    viewer.Offset = new Vector(double.MaxValue, 0);
                    window.UpdateLayout();
                    Assert.True(viewer.Offset.X > 0);
                }
                Assert.InRange(codeScroll.Bounds.Height, 1, 360);
                Assert.True(codeScroll.Extent.Height > codeScroll.Viewport.Height);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
