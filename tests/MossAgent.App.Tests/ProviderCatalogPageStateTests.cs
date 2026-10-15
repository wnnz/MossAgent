using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderCatalogPageStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LoadingAndFailureNeverDisplayTheEmptyCard_AndKeyboardRetryRecovers(bool dark) => _session.DispatchAsync(async () =>
    {
        var delayed = new TaskCompletionSource<IReadOnlyList<AiProvider>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new ModelPickerProbeRepository { ProviderLoader = () => delayed.Task };
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var pane = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = models } };
        var window = Window(pane, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            var empty = pane.FindControl<AppCard>("EmptyCatalog")!;
            var notice = pane.FindControl<AppCard>("CatalogNotice")!;
            var rows = pane.FindControl<AppItemsControl>("ProviderRows")!;
            var refresh = Assert.Single(pane.GetVisualDescendants().OfType<AppActionButton>(), button => button.Label == "刷新");
            Assert.True(notice.IsVisible);
            Assert.False(empty.IsVisible);
            Assert.False(rows.IsEnabled);
            Assert.False(refresh.IsEffectivelyEnabled);
            delayed.SetException(new InvalidOperationException("fixture-secret"));
            await models.Catalog.RefreshAsync();
            window.UpdateLayout();
            Assert.True(notice.IsVisible);
            Assert.False(empty.IsVisible);
            Assert.True(refresh.IsEffectivelyEnabled);
            Assert.DoesNotContain("fixture-secret", Assert.Single(notice.GetVisualDescendants().OfType<AppText>()).Text);
            repository.ProviderLoader = null;
            PressSpace(refresh);
            await providers.RefreshCommand.ExecutionTask!;
            window.UpdateLayout();
            Assert.True(empty.IsVisible);
            Assert.False(notice.IsVisible);
            Assert.True(rows.IsEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedReloadRetainsRowsAndEditorDraftButDisablesMutationUntilRetry(bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = new ModelPickerProbeRepository();
        var provider = ProviderSettingsRowTestData.Provider();
        var model = ProviderSettingsRowTestData.Model(provider.Id);
        repository.Providers.Add(provider);
        repository.Models.Add(model);
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        var pane = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = models } };
        var window = Window(pane, dark);
        models.ToggleProviderCommand.Execute(models.ProviderGroups[0]);
        models.EditProviderModelCommand.Execute(model);
        models.ModelId = "unsaved-draft";
        window.Show();
        try
        {
            repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(new Exception("fixture-secret"));
            await providers.RefreshCommand.ExecuteAsync(null);
            window.UpdateLayout();
            var row = Assert.Single(pane.GetVisualDescendants().OfType<AppModelSettingsRow>());
            Assert.False(row.IsEffectivelyEnabled);
            Assert.All(pane.GetVisualDescendants().OfType<AppButton>().Where(button => button.Classes.Contains("provider-settings-action")),
                button => Assert.Equal(0.5, button.Opacity));
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(), button => Equals(button.Content, "保存模型"));
            Assert.False(save.IsEffectivelyEnabled);
            Assert.True(models.IsFormOpen);
            Assert.Equal("unsaved-draft", models.ModelId);
            Assert.Contains(pane.GetVisualDescendants().OfType<AppText>(), text => text.IsEffectivelyVisible && text.Text == models.EditorNotice);
            repository.ModelLoader = null;
            await providers.RefreshCommand.ExecuteAsync(null);
            window.UpdateLayout();
            Assert.True(save.IsEffectivelyEnabled);
            Assert.All(pane.GetVisualDescendants().OfType<AppButton>().Where(button => button.Classes.Contains("provider-settings-action")),
                button => Assert.Equal(1, button.Opacity));
            Assert.Equal("unsaved-draft", models.ModelId);
            Assert.True(models.IsFormOpen);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Window(ProviderAndModelSettingsPane pane, bool dark) => new()
    {
        Width = 980,
        Height = 700,
        Content = pane,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
