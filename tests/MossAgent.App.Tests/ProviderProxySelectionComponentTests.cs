using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Styling;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderProxySelectionComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReloadingProxyNames_UpdatesTheBoundSelectorWithoutDroppingSelection(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = new ProxyReloadProbeRepository();
        var proxy = new ProxyProfile(Guid.NewGuid(), "before", ProxyProtocol.Http, "127.0.0.1", 8080, null, null, true, true);
        repository.Proxies.Add(proxy);
        var settings = new ProviderSettingsViewModel(repository);
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = "draft";
        var selector = CreateSelector(settings);
        var window = new AppWindow
        {
            Width = 400,
            Height = 200,
            Content = selector,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Assert.Same(settings.SelectedProxy, selector.SelectedItem);
            repository.Proxies[0] = proxy with { Name = "after" };

            await settings.ReloadProxiesAsync();

            Assert.Equal("after", settings.SelectedProxy?.Name);
            Assert.Same(settings.SelectedProxy, selector.SelectedItem);
            Assert.Same(settings.Proxies, selector.ItemsSource);
            Assert.Equal("draft", settings.Name);
            selector.SelectedItem = Assert.Single(settings.Proxies, item => item.Id is null);
            Assert.Null(settings.SelectedProxy?.Id);
            Assert.NotNull(settings.SelectedProxy);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisablingAndRestoringTheChoice_RebindsTheSameIdentityInTheRealControl(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = new ProxyReloadProbeRepository();
        var proxy = new ProxyProfile(Guid.NewGuid(), "selected", ProxyProtocol.Http, "127.0.0.1", 8080, null, null, true, true);
        repository.Proxies.Add(proxy);
        var settings = new ProviderSettingsViewModel(repository);
        settings.OpenCreateFormCommand.Execute(null);
        var selector = CreateSelector(settings);
        var window = new AppWindow
        {
            Width = 400,
            Height = 200,
            Content = selector,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            repository.Proxies[0] = proxy with { IsEnabled = false };
            await settings.ReloadProxiesAsync();

            Assert.Null(selector.SelectedItem);
            Assert.Null(settings.SelectedProxy);
            Assert.True(settings.HasValidationError);
            repository.Proxies[0] = proxy;
            await settings.ReloadProxiesAsync();

            Assert.Equal(proxy.Id, settings.SelectedProxy?.Id);
            Assert.Same(settings.SelectedProxy, selector.SelectedItem);
            Assert.False(settings.HasValidationError);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    private static AppComboBox CreateSelector(ProviderSettingsViewModel settings)
    {
        var selector = new AppComboBox { DataContext = settings };
        selector.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(settings.Proxies)));
        selector.Bind(AppComboBox.SelectedItemProperty, new Binding(nameof(settings.SelectedProxy))
        {
            Mode = BindingMode.TwoWay
        });
        return selector;
    }
}
