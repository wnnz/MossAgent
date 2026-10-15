using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PageFocusViewportTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task InitialAndRememberedFocusSkipFieldsBelowTheViewport(int fieldKind) => _session.Dispatch(() =>
    {
        Control field = fieldKind switch
        {
            0 => new AppInput { Text = "Unchanged draft" },
            1 => new AppComboBox { ItemsSource = new[] { "First", "Second" }, SelectedIndex = 0 },
            _ => new AppNumericUpDown { Value = 12 }
        };
        var first = new AppButton { Content = "First" };
        var content = new StackPanel { Children = { first, new Grid { Height = 400 }, field } };
        var scroll = new AppScrollViewer { Content = content };
        var window = new AppWindow { Width = 470, Height = 300, Content = scroll };
        window.Show();
        try
        {
            Flush(window);
            var target = field is AppNumericUpDown ? field.GetVisualDescendants().OfType<TextBox>().Single() : field;
            Assert.Same(first, AppPageSwitcher.SelectFocusTarget(scroll, null));
            Assert.Same(first, AppPageSwitcher.SelectFocusTarget(scroll, target));
            Assert.False(AppPageSwitcher.IsAvailableFocusTarget(target));
            Assert.Equal(0, scroll.Offset.Y);

            Assert.True(target.Focus());
            Flush(window);
            Assert.True(scroll.Offset.Y > 0);
            Assert.True(AppPageSwitcher.IsAvailableFocusTarget(target));
            Assert.Same(target, AppPageSwitcher.SelectFocusTarget(scroll, target));
            Assert.Same(field, AppPageSwitcher.SelectFocusTarget(scroll, null));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PartiallyClippedFieldsAreNotAutomaticFocusTargets(bool horizontal) => _session.Dispatch(() =>
    {
        var field = new AppInput { Width = 200, Height = 40 };
        var content = new Canvas { Width = 700, Height = 700, Children = { field } };
        Canvas.SetLeft(field, horizontal ? 400 : 0);
        Canvas.SetTop(field, horizontal ? 0 : 280);
        var scroll = new AppScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var window = new AppWindow { Width = 470, Height = 300, Content = scroll };
        window.Show();
        try
        {
            Flush(window);
            Assert.False(AppPageSwitcher.IsAvailableFocusTarget(field));
            Assert.Null(AppPageSwitcher.SelectFocusTarget(scroll, field));
            Assert.Equal(default, scroll.Offset);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AFieldInsideANestedViewportMustAlsoFitItsOuterViewport() => _session.Dispatch(() =>
    {
        var field = new AppInput();
        var nested = new AppScrollViewer { Height = 100, Content = field };
        var content = new StackPanel { Children = { new Grid { Height = 400 }, nested } };
        var scroll = new AppScrollViewer { Content = content };
        var window = new AppWindow { Width = 470, Height = 300, Content = scroll };
        window.Show();
        try
        {
            Flush(window);
            Assert.True(field.Bounds.Height <= nested.Viewport.Height);
            Assert.False(AppPageSwitcher.IsAvailableFocusTarget(field));
            Assert.Null(AppPageSwitcher.SelectFocusTarget(scroll, field));
            Assert.Equal(default, scroll.Offset);
            Assert.Equal(default, nested.Offset);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
