using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ToolDetailsKeyboardTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, false)]
    [InlineData(740, false)]
    [InlineData(280, true)]
    [InlineData(740, true)]
    public Task ExpandedOutput_IsKeyboardScrollableWithoutMovingTheCard(double width, bool dark) => _session.Dispatch(() =>
    {
        var output = string.Join('\n', Enumerable.Range(1, 80).Select(index => $"检查结果 {index}"));
        var row = ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", true, "已完成", null,
            [new ToolDialogueMessage("工具 · filesystem.read_file", output)]));
        var view = new WorkspaceMessageView { DataContext = row };
        var outside = new AppButton { Content = "其他操作" };
        var host = new StackPanel { Children = { view, outside } };
        var window = new Window
        {
            Width = width,
            Height = 480,
            Content = host,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var viewer = Assert.Single(view.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.True(viewer.Extent.Height > viewer.Viewport.Height);
            Assert.Equal(row.ToolSummary, AutomationProperties.GetName(viewer));
            Assert.False(new AppScrollViewer().Focusable);
            var toggle = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Name == "ToolButton");
            Assert.True(toggle.Focus(NavigationMethod.Tab));
            Press(window, Key.Tab);
            Assert.True(viewer.IsFocused);
            var bounds = view.Bounds;
            Press(window, Key.PageDown);
            Assert.True(viewer.Offset.Y > 0);
            Assert.Equal(bounds, view.Bounds);
            var pages = (int)Math.Ceiling(viewer.Extent.Height / viewer.Viewport.Height) + 1;
            for (var page = 0; page < pages; page++) Press(window, Key.PageDown);
            Assert.InRange(viewer.Offset.Y, viewer.Extent.Height - viewer.Viewport.Height - 1,
                viewer.Extent.Height - viewer.Viewport.Height + 1);
            for (var page = 0; page < pages; page++) Press(window, Key.PageUp);
            Assert.Equal(0, viewer.Offset.Y);
            Assert.True(outside.Focus(NavigationMethod.Tab));
            Press(window, Key.PageDown);
            Assert.Equal(0, viewer.Offset.Y);
            Assert.Equal(bounds, view.Bounds);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var physical = Enum.Parse<PhysicalKey>(key.ToString());
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
        window.UpdateLayout();
    }
}
