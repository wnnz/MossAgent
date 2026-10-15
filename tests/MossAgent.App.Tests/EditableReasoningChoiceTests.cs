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
public sealed class EditableReasoningChoiceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealEditor_TypingAndSelectingBothSaveAndReopen(bool dark) => _session.DispatchAsync(async () =>
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.OpenAiCompatible };
        repository.Models[0] = model with { ModelId = "claude-opus-4-6" };
        using var providers = new ProviderSettingsViewModel(repository);
        using var editor = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        await editor.RefreshProvidersCommand.ExecuteAsync(null);
        var pane = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = editor } };
        var window = new AppWindow
        {
            Width = 980,
            Height = 780,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            editor.EditProviderModelCommand.Execute(repository.Models[0]);
            window.UpdateLayout();
            var choice = Assert.Single(pane.GetVisualDescendants().OfType<AppEditableChoice>());
            var input = choice.FindControl<AppInput>("ChoiceInput")!;
            Assert.Equal(new[] { "low", "medium", "high", "max" }, choice.Choices);
            Assert.True(input.Focus(NavigationMethod.Tab));
            input.SelectAll();
            window.KeyTextInput("vendor-extreme_2");
            Assert.Equal("vendor-extreme_2", editor.ReasoningEffort);
            Assert.False(editor.HasReasoningError);
            await editor.SaveModelCommand.ExecuteAsync(null);
            Assert.Equal("vendor-extreme_2", Assert.Single(repository.Models).ReasoningEffort);
            editor.EditProviderModelCommand.Execute(repository.Models[0]);
            window.UpdateLayout();
            Assert.Equal("vendor-extreme_2", input.Text);
            Assert.True(input.Focus());
            window.KeyPress(Key.F4, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.F4, RawInputModifiers.None, PhysicalKey.None, null);
            var button = choice.FindControl<AppIconButton>("ChoiceButton")!;
            var flyout = Assert.IsType<AppFlyout>(button.Flyout);
            Assert.True(flyout.IsOpen);
            var list = Assert.IsType<AppListBox>(flyout.Content);
            list.SelectedItem = "high";
            Assert.Equal("high", editor.ReasoningEffort);
            Assert.False(flyout.IsOpen);
            await editor.SaveModelCommand.ExecuteAsync(null);
            Assert.Equal("high", Assert.Single(repository.Models).ReasoningEffort);
            editor.EditProviderModelCommand.Execute(repository.Models[0]);
            Assert.Equal("high", input.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ChoicesRefresh_DoesNotEraseTypedDraftAndDetachClosesPopup(bool dark) => _session.Dispatch(() =>
    {
        var choice = new AppEditableChoice { Choices = ["low", "medium", "high"], SelectedChoice = "vendor-custom" };
        var window = new AppWindow
        {
            Width = 360,
            Height = 160,
            Content = choice,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var input = choice.FindControl<AppInput>("ChoiceInput")!;
            var button = choice.FindControl<AppIconButton>("ChoiceButton")!;
            choice.Choices = ["low", "high"];
            Assert.Equal("vendor-custom", choice.SelectedChoice);
            Assert.Equal("vendor-custom", input.Text);
            Assert.Equal(32, button.Bounds.Width);
            Assert.Equal(32, button.Bounds.Height);
            Assert.True(input.Focus());
            window.KeyPress(Key.F4, RawInputModifiers.None, PhysicalKey.None, null);
            var flyout = Assert.IsType<AppFlyout>(button.Flyout);
            Assert.True(flyout.IsOpen);
            window.Content = null;
            Assert.False(flyout.IsOpen);
            Assert.Equal("vendor-custom", choice.SelectedChoice);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
