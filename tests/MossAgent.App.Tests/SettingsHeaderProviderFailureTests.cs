using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsHeaderProviderFailureTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DiscoveryFailure_ReachesReadableSharedHeaderWithoutExposingSecrets(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var discovery = new DeferredModelDiscoveryService();
        using var models = new ModelSettingsViewModel(repository, discovery, providers);
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        var pane = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = models } };
        var window = new AppWindow
        {
            Width = 486,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            var fetch = models.FetchModelsForProviderCommand.ExecuteAsync(Assert.Single(models.ProviderGroups));
            await discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            discovery.Fail(new InvalidOperationException("synthetic-private-provider-key"));
            await fetch.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            window.UpdateLayout();
            var header = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsPageHeader>());
            Assert.Equal(models.Status, header.Status);
            Assert.True(header.HasStatus);
            Assert.True(header.IsStatusError);
            var status = Assert.Single(header.GetVisualDescendants().OfType<AppText>(), text => text.Text == models.Status);
            Assert.True(status.IsEffectivelyVisible);
            Assert.True(status.TextLayout.WidthIncludingTrailingWhitespace <= status.Bounds.Width + 1);
            Assert.Equal(Color.Parse(dark ? "#F87171" : "#B42318"), Assert.IsAssignableFrom<ISolidColorBrush>(status.Foreground).Color);
            Assert.DoesNotContain("synthetic-private", string.Join(" ", header.GetVisualDescendants().OfType<AppText>().Select(text => text.Text)));
            Assert.False(models.IsFormOpen);
            Assert.False(providers.IsFormOpen);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
