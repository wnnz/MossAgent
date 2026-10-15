using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsContentAlignmentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1919, 746, 728)]
    [InlineData(true, 1919, 746, 728)]
    [InlineData(false, 1280, 427, 728)]
    [InlineData(true, 1280, 427, 728)]
    [InlineData(false, 980, 344, 594)]
    [InlineData(true, 980, 344, 594)]
    public Task AppearanceContentFitsTheSharedShellAndPreservesInternalAnchors(bool dark, int width, int left, int contentWidth) =>
        _session.Dispatch(() =>
        {
            Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var settings = new SettingsOverlay { DataContext = AppearanceContext() };
            var shell = new Grid { RowDefinitions = new RowDefinitions("44,*") };
            Grid.SetRow(settings, 1);
            shell.Children.Add(settings);
            var window = new AppWindow { Width = width, Height = 1032, Content = shell };
            window.Show();
            try
            {
                window.UpdateLayout();
                var column = settings.GetVisualDescendants().OfType<Grid>()
                    .First(control => control.Classes.Contains("settings-content-column"));
                Assert.Equal(new Point(left, 118), column.TranslatePoint(default, window));
                Assert.Equal(contentWidth, column.Bounds.Width);
                var appearance = settings.GetVisualDescendants().OfType<AppearanceSettingsPane>().Single();
                var title = appearance.GetVisualDescendants().OfType<AppText>()
                    .Single(control => control.Classes.Contains("settings-title"));
                Assert.Equal(new Point(left, 118), title.TranslatePoint(default, window));
                Assert.Equal(28, title.FontSize);
                var heading = appearance.GetVisualDescendants().OfType<AppText>()
                    .Single(control => control.Classes.Contains("settings-theme-heading"));
                Assert.Equal(new Thickness(0, 41, 0, 16), heading.Margin);
                var previewTop = heading.TranslatePoint(default, window)!.Value.Y + heading.Bounds.Height + 16;
                var choices = appearance.GetVisualDescendants().OfType<AppButton>()
                    .Where(control => control.Classes.Contains("theme-mode-button")).ToArray();
                Assert.Equal(3, choices.Length);
                var picker = appearance.GetVisualDescendants().OfType<AppThemeModePicker>().Single();
                Assert.Equal(previewTop, picker.TranslatePoint(default, window)!.Value.Y);
                Assert.All(choices, choice => Assert.Equal(new Size(80, 60), choice.Bounds.Size));
                Assert.All(choices, choice => Assert.Equal(previewTop + 9, choice.TranslatePoint(default, window)!.Value.Y));
                Assert.Equal(left + contentWidth - 289, choices[0].TranslatePoint(default, window)!.Value.X);
                var paletteTop = previewTop + picker.Bounds.Height + 16;
                var palette = appearance.GetVisualDescendants().OfType<AppThemePaletteCard>().Single();
                Assert.Equal(new Point(left, paletteTop), palette.TranslatePoint(default, window));
                Assert.Equal(new Size(contentWidth, 226), palette.GetVisualDescendants().OfType<AppCard>().Single().Bounds.Size);
                var advanced = appearance.GetVisualDescendants().OfType<AppExpander>().Single();
                Assert.False(advanced.IsExpanded);
                Assert.DoesNotContain(appearance.GetVisualDescendants().OfType<AppDiffPreview>(), preview => preview.IsEffectivelyVisible);
                advanced.IsExpanded = true;
                window.UpdateLayout();
                var diff = appearance.GetVisualDescendants().OfType<AppDiffPreview>().Single();
                Assert.Equal(new Size(contentWidth, 125), diff.Bounds.Size);
                var scroll = appearance.GetVisualDescendants().OfType<AppScrollViewer>()
                    .Single(control => control.Classes.Contains("settings-page-scroll"));
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                Assert.InRange(scroll.Extent.Width, 0, scroll.Viewport.Width);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static object AppearanceContext() => new
    {
        IsAppearanceSettingsSelected = true,
        IsPersonalSettingsMatch = true,
        IsIntegrationSettingsMatch = true,
        IsAgentSectionSettingsMatch = true,
        IsBrowserSettingsSelected = false,
        IsSubagentSettingsSelected = false,
        IsAgentSettingsSelected = false,
        IsProviderSettingsSelected = false,
        IsProxySettingsSelected = false,
        IsMcpSettingsSelected = false,
        IsSystemThemeSelected = true,
        IsLightThemeSelected = false,
        IsDarkThemeSelected = false
    };
}
