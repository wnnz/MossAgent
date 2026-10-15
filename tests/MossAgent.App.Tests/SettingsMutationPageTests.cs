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
public sealed class SettingsMutationPageTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task SaveButtonFailureKeepsEditorAndSafeError_ThenKeyboardRetryClosesIt(bool provider, bool dark) => _session.DispatchAsync(async () =>
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        using var models = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        if (provider) providers.EditProviderCommand.Execute(repository.Store.Providers[0]);
        else models.EditProviderModelCommand.Execute(repository.Store.Models[0]);
        repository.FailingOperation = provider ? "save-provider" : "save-model";
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
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(),
                button => Equals(button.Content, provider ? "保存提供商" : "保存模型"));
            var command = provider ? providers.SaveCommand : models.SaveModelCommand;
            PressSpace(save);
            await command.ExecutionTask!;
            window.UpdateLayout();
            var status = provider ? providers.Status : models.Status;
            Assert.Contains("无法保存", status);
            Assert.DoesNotContain("fixture-secret", status);
            Assert.True(provider ? providers.IsFormOpen : models.IsFormOpen);
            Assert.True(save.IsEnabled);
            Assert.Contains(pane.GetVisualDescendants().OfType<AppText>(), text => text.IsEffectivelyVisible && text.Text == status);
            repository.FailingOperation = null;
            PressSpace(save);
            await command.ExecutionTask!;
            Assert.False(provider ? providers.IsFormOpen : models.IsFormOpen);
            Assert.False(provider ? providers.HasValidationError : models.HasStatusError);
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
