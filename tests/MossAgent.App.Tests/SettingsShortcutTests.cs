using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsShortcutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task ControlCommaOpensIdempotentlyAndPreservesPageAndDraft(bool dark, bool compact) => WithWindow(dark, compact, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        fixture.Main.SettingsSearchQuery = "外观";
        fixture.Main.CloseSettingsCommand.Execute(null);
        fixture.Workspace.ComposerText = "unsent draft";
        Flush(window);
        var composer = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(), input => input.IsEffectivelyVisible && input.AcceptsReturn);
        Assert.True(composer.Focus());
        Press(window, RawInputModifiers.None);
        Assert.False(fixture.Main.IsSettingsOpen);
        Press(window, RawInputModifiers.Control);
        Assert.True(fixture.Main.IsSettingsOpen);
        var navigation = window.FindControl<SettingsOverlay>("SettingsView")!.Navigation;
        if (compact) navigation.ToggleNavigationCommand.Execute(null);
        Flush(window);
        var wasOverlayOpen = navigation.IsOverlayOpen;
        var search = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("settings-search"));
        Assert.True(search.Focus());
        Press(window, RawInputModifiers.Control);
        Assert.True(fixture.Main.IsSettingsOpen);
        Assert.Equal(5, fixture.Main.SettingsTabIndex);
        Assert.Equal("外观", fixture.Main.SettingsSearchQuery);
        Assert.Equal("外观", search.Text);
        Assert.Equal(wasOverlayOpen, navigation.IsOverlayOpen);
        Assert.Equal("unsent draft", fixture.Workspace.ComposerText);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ModalEditorDoesNotInvokeTheOwnerShortcut(bool dark) => WithWindow(dark, false, (window, fixture) =>
    {
        var editor = new AppInput { Text = "modal draft" };
        var dialog = new AppWindow { Width = 320, Height = 200, Content = editor };
        var completion = dialog.ShowDialog(window);
        try
        {
            dialog.UpdateLayout();
            Assert.Same(window, dialog.Owner);
            Assert.True(editor.Focus());
            Press(dialog, RawInputModifiers.Control);
            Assert.False(fixture.Main.IsSettingsOpen);
            Assert.Equal("modal draft", editor.Text);
            Assert.False(completion.IsCompleted);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { dialog.Close(); }
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
        try { Flush(window); action(window, fixture); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Press(Window window, RawInputModifiers modifiers)
    {
        window.KeyPress(Key.OemComma, modifiers, PhysicalKey.Comma, ",");
        window.KeyRelease(Key.OemComma, modifiers, PhysicalKey.Comma, ",");
        Flush(window);
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
