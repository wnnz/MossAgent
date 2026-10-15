using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerInputWrappingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 360, false)]
    [InlineData(true, 360, false)]
    [InlineData(false, 740, false)]
    [InlineData(true, 740, false)]
    [InlineData(false, 360, true)]
    [InlineData(true, 740, true)]
    public Task LongDraft_WrapsAndScrollsVerticallyWithoutChangingText(bool dark, double width, bool unbroken) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var content = string.Concat(Enumerable.Repeat(unbroken ? "long_path_segment_" : "中文输入框应当自动换行，保留所有内容。", 100));
            fixture.Workspace.ComposerText = content;
            var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
            var window = new AppWindow
            {
                Width = width,
                Height = 640,
                Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try { AssertWrappedDraft(window, view, content); }
            finally { window.Close(); }
            Assert.Equal(content, fixture.Workspace.ComposerText);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RegularSingleLineInputs_RetainHorizontalEditing() => _session.Dispatch(() =>
    {
        var input = new AppInput { Text = "https://localhost/" + new string('x', 500) };
        var window = new AppWindow { Width = 340, Height = 120, Content = input };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(TextWrapping.NoWrap, input.TextWrapping);
            Assert.False(input.AcceptsReturn);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("field-multiline", true)]
    [InlineData("field-prompt", true)]
    [InlineData("json-editor", false)]
    public Task HumanTextVariants_WrapWhileJsonRetainsCodeEditing(string variant, bool wraps) => _session.Dispatch(() =>
    {
        var content = string.Concat(Enumerable.Repeat("用于设置页面的长文本描述。", 100));
        var input = new AppInput { Text = content, AcceptsReturn = true, Classes = { variant } };
        var window = new AppWindow { Width = 340, Height = 220, Content = input };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(wraps ? TextWrapping.Wrap : TextWrapping.NoWrap, input.TextWrapping);
            Assert.Equal(content, input.Text);
            var presenter = Assert.Single(input.GetVisualDescendants().OfType<TextPresenter>());
            Assert.Equal(wraps, presenter.TextLayout.TextLines.Count > 1);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertWrappedDraft(AppWindow window, WorkspaceComposerView view, string content)
    {
        window.UpdateLayout();
        var input = Assert.Single(view.GetVisualDescendants().OfType<AppInput>(),
            candidate => candidate.Classes.Contains("composer-input"));
        input.Focus();
        input.CaretIndex = content.Length;
        window.UpdateLayout();
        var viewer = Assert.Single(input.GetVisualDescendants().OfType<ScrollViewer>());
        var presenter = Assert.Single(input.GetVisualDescendants().OfType<TextPresenter>());

        Assert.Equal(TextWrapping.Wrap, input.TextWrapping);
        Assert.True(input.AcceptsReturn);
        Assert.Equal(content, input.Text);
        Assert.True(presenter.TextLayout.TextLines.Count > 1);
        Assert.InRange(input.Bounds.Height, 36, 180);
        Assert.InRange(viewer.Extent.Width, 1, viewer.Viewport.Width + 1);
        Assert.True(viewer.Extent.Height > viewer.Viewport.Height);
        Assert.Equal(0, viewer.Offset.X);
        viewer.Offset = new Avalonia.Vector(0, double.MaxValue);
        window.UpdateLayout();
        Assert.True(viewer.Offset.Y > 0);
    }
}
