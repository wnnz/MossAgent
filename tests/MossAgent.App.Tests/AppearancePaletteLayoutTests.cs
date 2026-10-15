using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppearancePaletteLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 384)]
    [InlineData(true, 384)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task BasicPageShowsOnlyTheEffectivePaletteAndNoAdvancedContent(bool dark, double width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.SelectThemeCommand.Execute(dark ? "Dark" : "Light");
        var pane = new AppearanceSettingsPane { DataContext = fixture.Main };
        var window = new AppWindow { Width = width, Height = 900, Content = pane };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var palette = Assert.Single(pane.GetVisualDescendants().OfType<AppThemePaletteCard>());
            Assert.Same(dark ? fixture.Main.Palette.Dark : fixture.Main.Palette.Light, palette.DataContext);
            Assert.Equal(new Size(width, 226), palette.GetVisualDescendants().OfType<AppCard>().Single().Bounds.Size);
            Assert.All(pane.GetVisualDescendants().OfType<AppDiffPreview>(), preview => Assert.False(preview.IsEffectivelyVisible));
            Assert.All(pane.GetVisualDescendants().OfType<AppThemeExtendedFontsCard>(), fonts => Assert.False(fonts.IsEffectivelyVisible));
            var advanced = pane.GetVisualDescendants().OfType<AppExpander>().Single();
            Assert.False(advanced.IsExpanded);
            advanced.IsExpanded = true;
            window.UpdateLayout();
            var diff = pane.GetVisualDescendants().OfType<AppDiffPreview>().Single();
            Assert.True(diff.IsEffectivelyVisible);
            Assert.Equal(new Size(width, 125), diff.Bounds.Size);
            Assert.True(pane.GetVisualDescendants().OfType<AppThemeExtendedFontsCard>().Single().IsEffectivelyVisible);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
