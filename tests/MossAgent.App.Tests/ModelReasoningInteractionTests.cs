using Avalonia;
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
public sealed class ModelReasoningInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SwitchingModel_DoesNotClampBackIntoTheDraftAndHomeCanCorrectFirstStep(bool dark) => _session.DispatchAsync(async () =>
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var gemini = provider with { Id = Guid.NewGuid(), Protocol = ProviderProtocol.GeminiGenerateContent, IsDefault = false };
        var flash = model with { Id = Guid.NewGuid(), ProviderId = gemini.Id, ModelId = "gemini-3.8-flash" };
        repository.Providers.Add(gemini);
        repository.Models.Add(flash);
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.SelectedReasoning = "Ultra";
        var view = new ModelPickerFlyoutView { DataContext = picker };
        var window = new AppWindow
        {
            Width = 280,
            Height = 420,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var component = Assert.Single(view.GetVisualDescendants().OfType<AppDiscreteSlider>());
            var slider = component.FindControl<AppSlider>("StepSlider")!;
            Assert.Equal(7, slider.Maximum);
            Assert.Equal(7, slider.Value);
            Assert.True(picker.TrySelect(gemini.Id, flash.Id));
            window.UpdateLayout();
            Assert.Equal(2, slider.Maximum);
            Assert.Equal(3, component.GetVisualDescendants().OfType<AppBorder>().Count(border => border.Classes.Contains("reasoning-tick")));
            Assert.Equal("Ultra", picker.SelectedReasoning);
            Assert.Equal("Ultra", component.SelectedStep);
            Assert.True(picker.HasReasoningError);
            Assert.False(picker.CanSendWithSelectedModel);
            Assert.True(picker.ResetReasoningCommand.CanExecute(null));
            Assert.Equal("medium", repository.Models.Single(item => item.Id == flash.Id).ReasoningEffort);

            Assert.True(slider.Focus());
            window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Home, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("low", picker.EffectiveReasoningEffort);
            window.UpdateLayout();
            var tick = component.GetVisualDescendants().OfType<AppBorder>().First(border => border.Classes.Contains("reasoning-tick"));
            var thumb = Assert.Single(slider.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>());
            var tickCenter = tick.TranslatePoint(default, component)!.Value.X + tick.Bounds.Width / 2;
            var thumbCenter = thumb.TranslatePoint(default, component)!.Value.X + thumb.Bounds.Width / 2;
            Assert.True(Math.Abs(tickCenter - thumbCenter) <= 0.5,
                $"Tick={tickCenter} ({tick.Bounds}); thumb={thumbCenter} ({thumb.Bounds}); slider={slider.Bounds}");
            Assert.False(picker.HasReasoningError);
            Assert.True(picker.CanSendWithSelectedModel);
            picker.ResetReasoningCommand.Execute(null);
            Assert.Equal("默认", picker.SelectedReasoning);
            Assert.Equal(1, slider.Value);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, "gemini-3-pro-preview", "medium")]
    [InlineData(true, "gemini-3-pro-preview", "medium")]
    [InlineData(false, "gemini-3.8-flash", "minimal")]
    [InlineData(true, "gemini-3.8-flash", "minimal")]
    public Task RealModelEditor_OptionsRefreshPreservesLegacySelectionAndKeyboardCorrection(bool dark, string modelId, string legacy) => _session.DispatchAsync(async () =>
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers[0] = provider with { Protocol = ProviderProtocol.GeminiGenerateContent };
        repository.Models[0] = model with { ModelId = modelId, ReasoningEffort = legacy };
        using var providers = new ProviderSettingsViewModel(repository);
        using var editor = new ModelSettingsViewModel(repository, new FixedModelDiscoveryService([]), providers);
        await editor.RefreshProvidersCommand.ExecuteAsync(null);
        var view = new ProviderAndModelSettingsPane { DataContext = new { Providers = providers, Models = editor } };
        var window = new AppWindow
        {
            Width = 980,
            Height = 700,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            editor.EditProviderModelCommand.Execute(repository.Models[0]);
            window.UpdateLayout();
            var combo = Assert.Single(view.GetVisualDescendants().OfType<AppEditableChoice>());
            var input = combo.FindControl<AppInput>("ChoiceInput")!;
            Assert.Equal(legacy, combo.SelectedChoice);
            Assert.Equal(legacy, editor.ReasoningEffort);
            Assert.True(editor.HasReasoningError);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(input.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("high", editor.ReasoningEffort);
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            var corrected = modelId == "gemini-3-pro-preview" ? "low" : "medium";
            Assert.Equal(corrected, editor.ReasoningEffort);
            Assert.False(editor.HasReasoningError);
            Assert.Equal(corrected, combo.SelectedChoice);
            await editor.SaveModelCommand.ExecuteAsync(null);
            Assert.False(editor.HasStatusError, editor.Status);
            Assert.Equal(corrected, Assert.Single(repository.Models).ReasoningEffort);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 360)]
    [InlineData(true, 360)]
    [InlineData(false, 740)]
    [InlineData(true, 740)]
    public Task Composer_BlocksUnsupportedEffortAndPreservesDraftUntilCorrection(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await fixture.Configurations.SaveProviderAsync(fixture.SelectedProvider with { Protocol = ProviderProtocol.GeminiGenerateContent }, fixture.Token);
        var gemini = fixture.SelectedModel with { Id = Guid.NewGuid(), ModelId = "gemini-3.8-flash", IsDefault = false };
        await fixture.Configurations.SaveModelAsync(gemini, fixture.Token);
        var workspace = fixture.Workspace;
        await workspace.ModelPicker.ReloadAsync();
        Assert.True(workspace.ModelPicker.TrySelect(gemini.ProviderId, gemini.Id));
        workspace.ModelPicker.SelectedReasoning = "最低";
        workspace.ComposerText = "retained-draft";
        var view = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow
        {
            Width = width,
            Height = 640,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var send = Assert.Single(view.GetVisualDescendants().OfType<AppIconButton>(), button => button.Name == "SendButton");
            var notice = Assert.Single(view.GetVisualDescendants().OfType<AppText>(), text => text.Text == workspace.ModelPicker.ReasoningNotice);
            Assert.False(workspace.PrimaryActionCommand.CanExecute(null));
            Assert.False(send.IsEffectivelyEnabled);
            Assert.True(notice.IsEffectivelyVisible);
            Assert.InRange(notice.TranslatePoint(default, view)!.Value.X + notice.Bounds.Width, 0, view.Bounds.Width + 0.5);
            workspace.ModelPicker.SelectedReasoning = "高";
            Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
            Assert.True(send.IsEffectivelyEnabled);
            Assert.False(notice.IsEffectivelyVisible);
            Assert.Equal("retained-draft", workspace.ComposerText);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
