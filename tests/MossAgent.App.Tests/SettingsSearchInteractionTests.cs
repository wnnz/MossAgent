using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsSearchInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SearchFiltersVisibleSectionsAndNavigatesWithoutLosingDrafts(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var draft = fixture.Workspace.ComposerText;
        fixture.Main.SelectSettingsTabCommand.Execute("0");
        fixture.Main.Providers.OpenCreateFormCommand.Execute(null);
        fixture.Main.Providers.Name = "未保存的搜索测试配置";
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = 980,
            Height = 640,
            WindowState = WindowState.Normal,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var settings = Assert.Single(window.GetVisualDescendants().OfType<SettingsOverlay>());
            var search = Assert.Single(settings.GetVisualDescendants().OfType<AppInput>(),
                control => control.Classes.Contains("settings-search"));
            search.Text = "不存在的合成设置";
            window.UpdateLayout();
            Assert.Equal(search.Text, fixture.Main.SettingsSearchQuery);
            Assert.Empty(VisibleNavigation(settings));
            Assert.Empty(VisibleHeadings(settings));
            Assert.True(Assert.Single(settings.GetVisualDescendants().OfType<AppText>(),
                control => control.Text == "没有匹配的设置").IsEffectivelyVisible);
            Assert.True(fixture.Main.Providers.IsFormOpen);
            search.Text = " BROWSER ";
            window.UpdateLayout();
            Assert.Equal("浏览器", Assert.Single(VisibleNavigation(settings)).Label);
            Assert.Equal("集成", Assert.Single(VisibleHeadings(settings)).Text);
            Assert.True(fixture.Main.IsProviderSettingsSelected);
            var browser = Assert.Single(VisibleNavigation(settings));
            Assert.True(browser.Focus(NavigationMethod.Tab));
            browser.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            browser.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            window.UpdateLayout();
            Assert.True(fixture.Main.IsBrowserSettingsSelected);
            Assert.True(Assert.Single(settings.GetVisualDescendants().OfType<BrowserSettingsPane>()).IsEffectivelyVisible);
            search.Text = string.Empty;
            window.UpdateLayout();
            Assert.Equal(7, VisibleNavigation(settings).Length);
            Assert.Equal(3, VisibleHeadings(settings).Length);
            fixture.Main.SelectSettingsTabCommand.Execute("0");
            window.UpdateLayout();
            Assert.True(fixture.Main.Providers.IsFormOpen);
            Assert.Equal("未保存的搜索测试配置", fixture.Main.Providers.Name);
            Assert.Equal(draft, fixture.Workspace.ComposerText);
            Assert.False(fixture.Workspace.IsRunning);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppSettingsNavButton[] VisibleNavigation(SettingsOverlay settings) =>
        settings.GetVisualDescendants().OfType<AppSettingsNavButton>().Where(control => control.IsEffectivelyVisible).ToArray();

    private static AppText[] VisibleHeadings(SettingsOverlay settings) =>
        settings.GetVisualDescendants().OfType<AppText>()
            .Where(control => control.IsEffectivelyVisible && control.Classes.Contains("settings-navigation-heading")).ToArray();
}
