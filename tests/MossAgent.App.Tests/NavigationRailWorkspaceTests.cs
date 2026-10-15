using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationRailWorkspaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HomeButton_UsesMeasuredFilledInkAndSelectionSurface(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        var home = RailButton(window, "新对话");
        var icon = Assert.Single(home.GetVisualDescendants().OfType<AppIcon>());
        Assert.Equal(17, icon.Size);
        Assert.True(home.IsFilledIcon);
        Assert.Equal(new Size(36, 36), home.Bounds.Size);
        Assert.Equal(new Point(26, 70), Center(home, window));
        Assert.Equal(new Matrix(1, 0, 0, 1, -1, -0.5), icon.RenderTransform?.Value ?? Matrix.Identity);
        Assert.Equal(new Point(25.5, 70),
            icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), window)!.Value);
        var color = Assert.IsAssignableFrom<ISolidColorBrush>(home.Background).Color;
        Assert.Equal(Color.Parse(dark ? "#2C333A" : "#E2E9ED"), color);
        Assert.Same(fixture.Main.Pages.NewConversationCommand, home.Command);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FooterSettings_UsesTheReferenceBottomInset(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        var settings = RailButton(window, "设置");
        Assert.Equal(new Point(26, window.ClientSize.Height - 26), Center(settings, window));
        Assert.Same(fixture.Main.ToggleSettingsCommand, settings.Command);
        var automation = RailButton(window, "定时任务");
        Assert.Equal(new Point(26, 114), Center(automation, window));
        Assert.True(automation.IsEffectivelyEnabled);
        Assert.Same(fixture.Main.Pages.OpenAutomationsCommand, automation.Command);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RailActions_KeepExistingCommandsAndDoNotSendTheDraft(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        var workspace = fixture.Workspace;
        var project = workspace.Catalog.SelectedProject;
        var task = workspace.Catalog.SelectedTask;
        var draft = workspace.ComposerText;
        Activate(RailButton(window, "审计与执行记录"));
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.True(fixture.Main.IsInspectorAudit);
        Activate(RailButton(window, "设置"));
        Assert.True(fixture.Main.IsSettingsOpen);
        fixture.Main.CloseSettingsCommand.Execute(null);
        Assert.Same(project, workspace.Catalog.SelectedProject);
        Assert.Same(task, workspace.Catalog.SelectedTask);
        Assert.Equal(draft, workspace.ComposerText);
        Assert.False(workspace.IsRunning);
        Assert.Empty(fixture.Server.Requests);
    });

    private Task WithWindow(bool dark, Action<MainWindow, WorkspaceModelSelectionFixture> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = Avalonia.Controls.WindowState.Normal,
            Width = 1280,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); action(window, fixture); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppIconButton RailButton(MainWindow window, string label) =>
        Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
            button => button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button") && button.Label == label);

    private static Point Center(AppIconButton button, MainWindow window) =>
        button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
