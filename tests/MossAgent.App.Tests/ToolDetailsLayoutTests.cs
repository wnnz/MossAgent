using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ToolDetailsLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, false, "Light")]
    [InlineData(740, false, "Light")]
    [InlineData(280, true, "Light")]
    [InlineData(740, true, "Light")]
    [InlineData(280, false, "Dark")]
    [InlineData(740, false, "Dark")]
    [InlineData(280, true, "Dark")]
    [InlineData(740, true, "Dark")]
    public Task LongToolText_WrapsWithoutExpandingTheDialogueViewport(double width, bool dialogue, string theme) =>
        _session.Dispatch(() =>
        {
            var output = "C:\\workspace\\" + new string('x', 2200) + "\\probe.txt\n完整工具结果";
            var content = dialogue
                ? (Control)new SubagentDialogueView
                {
                    DataContext = ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", true, "完成", null,
                        [new ToolDialogueMessage("工具 · filesystem.read_file", output)]))
                }
                : new AppSelectableText { Text = output, TextWrapping = TextWrapping.Wrap };
            var viewer = CreateViewer(content);
            var window = new Window
            {
                Width = width,
                Height = 600,
                Content = viewer,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Equal(ScrollBarVisibility.Disabled, viewer.HorizontalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Auto, viewer.VerticalScrollBarVisibility);
                Assert.Equal(260, viewer.MaxHeight);
                Assert.InRange(viewer.Viewport.Width, 1, width);
                Assert.InRange(viewer.Extent.Width, 0, viewer.Viewport.Width + 1);
                Assert.True(viewer.Extent.Height > viewer.Viewport.Height);
                var text = Assert.Single(viewer.GetVisualDescendants().OfType<AppSelectableText>());
                Assert.Equal(output, text.Text);
                Assert.True(text.Bounds.Height > 40);
                Assert.InRange(text.Bounds.Width, 1, viewer.Viewport.Width);
                viewer.Offset = new Vector(0, double.MaxValue);
                window.UpdateLayout();
                Assert.InRange(viewer.Offset.Y, 1, viewer.Extent.Height - viewer.Viewport.Height + 1);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public void ToolDetails_UseSharedSizingWithoutChangingCodeBlockScrolling()
    {
        var chat = XDocument.Load(Path.Combine(AppPath(), "Views", "WorkspaceMessageView.axaml"));
        var details = DetailScroll(chat);
        Assert.Contains("tool-details-scroll", details.Attribute("Classes")?.Value.Split(' ') ?? []);
        Assert.Null(details.Attribute("MaxHeight"));
        Assert.Null(details.Attribute("HorizontalScrollBarVisibility"));
        var response = XDocument.Load(Path.Combine(AppPath(), "Controls", "AppResponseContent.axaml"));
        var code = Assert.Single(response.Descendants(), element => element.Name.LocalName == "AppScrollViewer"
            && element.Elements().Any(child => child.Name.LocalName == "SyntaxHighlightedSelectableTextBlock"));
        Assert.Contains("response-code-scroll", code.Attribute("Classes")?.Value.Split(' ') ?? []);
        var styles = XDocument.Load(Path.Combine(AppPath(), "Styles", "ResponseContentVariants.axaml"));
        var style = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "ScrollViewer.response-code-scroll");
        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "HorizontalScrollBarVisibility"
            && element.Attribute("Value")?.Value == "Auto");
    }

    private static AppScrollViewer CreateViewer(Control content)
    {
        var markup = DetailScroll(XDocument.Load(Path.Combine(AppPath(), "Views", "WorkspaceMessageView.axaml")));
        var viewer = new AppScrollViewer { Content = content };
        foreach (var variant in markup.Attribute("Classes")?.Value.Split(' ') ?? []) viewer.Classes.Add(variant);
        if (markup.Attribute("HorizontalScrollBarVisibility") is { } horizontal)
            viewer.HorizontalScrollBarVisibility = Enum.Parse<ScrollBarVisibility>(horizontal.Value);
        if (markup.Attribute("MaxHeight") is { } height)
            viewer.MaxHeight = double.Parse(height.Value, CultureInfo.InvariantCulture);
        return viewer;
    }

    private static XElement DetailScroll(XDocument chat) => Assert.Single(chat.Descendants(), element =>
        element.Name.LocalName == "AppBorder" && element.Attribute("Classes")?.Value == "tool-detail-surface")
        .Descendants().Single(element => element.Name.LocalName == "AppScrollViewer");

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src", "MossAgent.App");
    }
}
