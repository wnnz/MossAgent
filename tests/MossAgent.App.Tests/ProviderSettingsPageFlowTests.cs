using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderSettingsPageFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ComposedRows_ExpandAndOpenTheCorrectProviderAndModelEditors(bool dark) => _session.DispatchAsync(async () =>
    {
        var first = ProviderSettingsRowTestData.Provider();
        var second = ProviderSettingsRowTestData.Provider(false) with { Name = "second", IsDefault = false };
        var firstModel = ProviderSettingsRowTestData.Model(first.Id);
        var secondModel = ProviderSettingsRowTestData.Model(second.Id);
        var repository = new ModelReadProbeConfigurationRepository([first, second], [firstModel, secondModel]);
        var providers = new ProviderSettingsViewModel(repository);
        var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        var pane = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = models } };
        var window = new AppWindow
        {
            Width = 980,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var header = Assert.Single(pane.GetVisualDescendants().OfType<AppProviderSettingsHeader>(), row => row.Provider?.Id == second.Id);
            PressSpace(header.FindControl<AppIconButton>("ToggleModelsButton")!);
            Assert.True(Assert.Single(models.ProviderGroups, group => group.Provider.Id == second.Id).IsExpanded);
            Assert.True(header.IsExpanded);
            window.UpdateLayout();
            var modelRow = Assert.Single(pane.GetVisualDescendants().OfType<AppModelSettingsRow>(), row => row.Model?.Id == secondModel.Id);
            Assert.Contains(secondModel.DisplayName, AutomationProperties.GetName(modelRow.FindControl<AppButton>("EditModelButton")!), StringComparison.Ordinal);
            PressSpace(modelRow.FindControl<AppButton>("EditModelButton")!);
            Assert.True(models.IsFormOpen);
            Assert.Equal(second.Id, models.SelectedProvider?.Id);
            Assert.Equal(secondModel.Id, models.SelectedModel?.Id);
            Assert.Equal(secondModel.ModelId, models.ModelId);
            models.CloseFormCommand.Execute(null);
            window.UpdateLayout();
            PressSpace(header.FindControl<AppButton>("EditProviderButton")!);
            Assert.True(providers.IsFormOpen);
            Assert.Equal(second.Id, providers.SelectedItem?.Id);
            Assert.False(models.IsFormOpen);
            providers.CloseFormCommand.Execute(null);
            Assert.False(providers.IsFormOpen);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
