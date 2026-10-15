using Avalonia;
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
public sealed class CompactNavigationWorkspaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WindowToggleUsesCompactDrawerWithoutChangingDraftOrInspectorPreference(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        var pane = window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        Assert.True(pane.IsCompact);
        Assert.False(pane.IsNavigationVisible);
        Assert.True(fixture.Main.IsLeftPanelOpen);
        var composer = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("composer-input"));
        var draft = fixture.Workspace.ComposerText;
        Assert.True(composer.Focus());
        Activate(Toggle(window));
        Flush(window);
        Assert.True(pane.IsOverlayOpen);
        AssertVisibleActionsFit(window, window);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Flush(window);
        Assert.False(pane.IsOverlayOpen);
        Assert.Same(composer, window.FocusManager!.GetFocusedElement());
        Assert.Equal(draft, fixture.Workspace.ComposerText);
        fixture.Main.IsRightPanelOpen = true;
        var workspace = window.FindControl<AppWorkspacePanes>("WorkspaceColumns")!;
        workspace.IsInspectorExpanded = true;
        Flush(window);
        Activate(Toggle(window));
        Flush(window);
        Assert.True(pane.IsOverlayOpen);
        Assert.True(workspace.IsInspectorExpanded);
        pane.CloseOverlayCommand.Execute(null);
        Flush(window);
        Assert.True(workspace.IsInspectorExpanded);
        AssertVisibleActionsFit(window, window);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false, "0")]
    [InlineData(true, "0")]
    [InlineData(false, "1")]
    [InlineData(true, "1")]
    [InlineData(false, "2")]
    [InlineData(true, "2")]
    [InlineData(false, "3")]
    [InlineData(true, "3")]
    [InlineData(false, "4")]
    [InlineData(true, "4")]
    [InlineData(false, "5")]
    [InlineData(true, "5")]
    [InlineData(false, "6")]
    [InlineData(true, "6")]
    public Task SettingsPagesKeepActionsReachableWithoutReservingSidebarWidth(bool dark, string tab) => WithWindow(dark, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute(tab);
        Flush(window);
        var settings = window.FindControl<SettingsOverlay>("SettingsView")!;
        Assert.True(settings.Navigation.IsCompact);
        Assert.False(settings.Navigation.IsNavigationVisible);
        Assert.Equal(470, settings.Navigation.PrimaryPane!.Bounds.Width);
        AssertVisibleActionsFit(settings, window);
        Activate(Toggle(window));
        Flush(window);
        Assert.True(settings.Navigation.IsOverlayOpen);
        var destination = Assert.Single(settings.GetVisualDescendants().OfType<AppSettingsNavButton>(), button => button.Label == "外观");
        Assert.True(AppNavigationPanes.GetDismissOnClick(destination));
        Activate(destination);
        Flush(window);
        Assert.False(settings.Navigation.IsOverlayOpen);
        Assert.True(fixture.Main.IsAppearanceSettingsSelected);
        AssertVisibleActionsFit(settings, window);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProviderDialogRemainsInsideCompactWindowAndEscapeClosesOnlyEditor(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("0");
        fixture.Main.Providers.OpenCreateFormCommand.Execute(null);
        Flush(window);
        var dialog = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialog>(), candidate => candidate.IsOpen);
        var surface = Assert.Single(dialog.GetVisualDescendants().OfType<AppCard>(), card => card.Name == "PART_Dialog");
        var origin = surface.TranslatePoint(default, window)!.Value;
        Assert.InRange(origin.X, 52, window.Width);
        Assert.True(origin.X + surface.Bounds.Width <= window.Width, $"Dialog ends at {origin.X + surface.Bounds.Width}.");
        AssertVisibleActionsFit(dialog, window);
        var focused = Assert.IsAssignableFrom<Control>(window.FocusManager!.GetFocusedElement());
        focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Flush(window);
        Assert.False(fixture.Main.Providers.IsFormOpen);
        Assert.True(fixture.Main.IsSettingsOpen);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AutomationListAndEditorFitCompactWindowWithoutRunningPlan(bool dark) => WithWindow(dark, async (window, fixture) =>
    {
        fixture.Main.Pages.OpenAutomationsCommand.Execute(null);
        var automations = fixture.Main.Pages.Automations!;
        await automations.ReloadAsync();
        await automations.CreateCommand.ExecuteAsync(null);
        automations.Editor.Name = "Compact fixture";
        automations.Editor.Prompt = "Synthetic draft; do not execute.";
        automations.Editor.SelectedProject = automations.Editor.Projects.First();
        automations.Editor.SelectedModel = automations.Editor.Models.First();
        await automations.SaveCommand.ExecuteAsync(null);
        Assert.False(automations.Editor.IsOpen);
        Assert.False(Assert.Single(automations.Items).Schedule.IsEnabled);
        Flush(window);
        var split = Assert.Single(window.GetVisualDescendants().OfType<AppResponsiveColumns>());
        Assert.True(split.IsCompact);
        Assert.Equal(split.Children[0].Bounds.Width, split.Children[1].Bounds.Width);
        Assert.True(split.Children[1].Bounds.Top >= split.Children[0].Bounds.Bottom);
        AssertVisibleActionsFit(window, window);
        await automations.EditCommand.ExecuteAsync(automations.Items[0]);
        Flush(window);
        var dialog = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialog>(), candidate => candidate.IsOpen);
        var surface = Assert.Single(dialog.GetVisualDescendants().OfType<AppCard>(), card => card.Name == "PART_Dialog");
        Assert.True(surface.Bounds.Width <= 422);
        AssertVisibleActionsFit(dialog, window);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Flush(window);
        Assert.False(automations.Editor.IsOpen);
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
        Assert.Empty(await fixture.Automations.GetRunsAsync(automations.Items[0].Schedule.Id, 50, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    });

    private Task WithWindow(bool dark, Action<MainWindow, WorkspaceModelSelectionFixture> action) =>
        WithWindow(dark, (window, fixture) => { action(window, fixture); return Task.CompletedTask; });

    private Task WithWindow(bool dark, Func<MainWindow, WorkspaceModelSelectionFixture, Task> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = 526,
            Height = 601,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            Assert.Equal(new Size(526, 601), window.ClientSize);
            await action(window, fixture);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppIconButton Toggle(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(), button => button.Label == "切换侧边栏");

    private static void Activate(Button button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertVisibleActionsFit(Control root, MainWindow window)
    {
        foreach (var button in root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible && button.Bounds.Width > 0))
        {
            var origin = button.TranslatePoint(default, window)!.Value;
            if (origin.Y >= window.Height || origin.Y + button.Bounds.Height <= 0) continue;
            Assert.True(origin.X >= 0 && origin.X + button.Bounds.Width <= window.Width + 0.01,
                $"{button.Name ?? button.Content?.ToString() ?? button.GetType().Name}: x={origin.X}, width={button.Bounds.Width}.");
        }
    }
}
