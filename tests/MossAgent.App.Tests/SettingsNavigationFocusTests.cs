using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsNavigationFocusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InitiallyCompactAppearanceKeepsItsHeadingInView(bool dark) => WithWindow(dark, true, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        AssertAtTop(window);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task VisibleRememberedEditorRetainsItsScrollAndDraftAfterEscape(bool dark) => WithWindow(dark, true, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var settings = window.FindControl<SettingsOverlay>("SettingsView")!;
        var scroll = AppearanceScroll(window);
        scroll.GetVisualDescendants().OfType<AppExpander>().Single().IsExpanded = true;
        Flush(window);
        var editor = scroll.GetVisualDescendants().OfType<AppComboBox>()
            .Single(control => AutomationProperties.GetName(control) == "主题代码字体");
        var selected = editor.SelectedItem;
        fixture.Main.ActivePalette.BackgroundHex = "#EFF3F7";
        Assert.True(editor.Focus());
        Flush(window);
        var offset = scroll.Offset;
        Assert.True(offset.Y > 0);
        settings.Navigation.ToggleNavigationCommand.Execute(null);
        Flush(window);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Flush(window);

        Assert.Same(editor, window.FocusManager!.GetFocusedElement());
        Assert.Equal(offset, scroll.Offset);
        Assert.Same(selected, editor.SelectedItem);
        Assert.Equal("#EFF3F7", fixture.Main.ActivePalette.BackgroundHex);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false, "selection")]
    [InlineData(true, "selection")]
    [InlineData(false, "escape")]
    [InlineData(true, "escape")]
    [InlineData(false, "outside")]
    [InlineData(true, "outside")]
    public Task ClosingDrawerDoesNotScrollToAnOffscreenRememberedField(bool dark, string dismissal) => WithWindow(dark, false, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var settings = window.FindControl<SettingsOverlay>("SettingsView")!;
        window.Height = 1032;
        AppearanceScroll(window).GetVisualDescendants().OfType<AppExpander>().Single().IsExpanded = true;
        Flush(window);
        var editor = settings.GetVisualDescendants().OfType<AppComboBox>()
            .Single(control => AutomationProperties.GetName(control) == "主题代码字体");
        editor.BringIntoView();
        Flush(window);
        Assert.True(AppPageSwitcher.IsAvailableFocusTarget(editor),
            $"Editor={editor.Bounds}; position={editor.TranslatePoint(default, AppearanceScroll(window))}; "
            + $"viewport={AppearanceScroll(window).Viewport}; offset={AppearanceScroll(window).Offset}; client={window.ClientSize}");
        Assert.True(editor.Focus());
        Flush(window);
        window.Width = 526;
        window.Height = 601;
        Flush(window);
        AppearanceScroll(window).Offset = default;
        Flush(window);
        AssertAtTop(window);
        Assert.True(editor.TranslatePoint(default, AppearanceScroll(window))!.Value.Y > AppearanceScroll(window).Viewport.Height);
        settings.Navigation.ToggleNavigationCommand.Execute(null);
        Flush(window);
        Assert.True(settings.Navigation.IsOverlayOpen);

        if (dismissal == "selection")
        {
            var destination = Assert.Single(settings.GetVisualDescendants().OfType<AppSettingsNavButton>(), button => button.Label == "外观");
            Assert.True(destination.Focus(NavigationMethod.Tab));
            destination.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            destination.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        }
        else if (dismissal == "escape") window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        else
        {
            window.MouseDown(new Point(490, 180), MouseButton.Left);
            window.MouseUp(new Point(490, 180), MouseButton.Left);
        }
        Flush(window);

        Assert.False(settings.Navigation.IsOverlayOpen);
        AssertAtTop(window);
        var focused = Assert.IsAssignableFrom<Control>(window.FocusManager!.GetFocusedElement());
        Assert.True(AppPageSwitcher.IsAvailableFocusTarget(focused));
        Assert.True(settings.Navigation.PrimaryPane!.IsVisualAncestorOf(focused));
        Assert.Empty(fixture.Server.Requests);
    });

    private Task WithWindow(bool dark, bool compact, Action<MainWindow, WorkspaceModelSelectionFixture> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = compact ? 526 : 1280,
            Height = compact ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            action(window, fixture);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppScrollViewer AppearanceScroll(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<AppearanceSettingsPane>())
        .GetVisualDescendants().OfType<AppScrollViewer>().Single(scroll => scroll.Classes.Contains("settings-page-scroll"));

    private static void AssertAtTop(MainWindow window)
    {
        var scroll = AppearanceScroll(window);
        Assert.Equal(0, scroll.Offset.Y);
        var heading = Assert.Single(scroll.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("settings-title"));
        var origin = heading.TranslatePoint(default, scroll)!.Value;
        Assert.InRange(origin.Y, 0, scroll.Viewport.Height - heading.Bounds.Height);
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
