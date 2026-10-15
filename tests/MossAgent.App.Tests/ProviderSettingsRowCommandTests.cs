using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderSettingsRowCommandTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task HeaderCommands_UseOwningProviderAndReflectExpansionAndAvailability() => _session.Dispatch(() =>
    {
        var provider = ProviderSettingsRowTestData.Provider();
        var group = new ProviderModelGroupViewModel(provider);
        AiProvider? edited = null;
        var header = new AppProviderSettingsHeader
        {
            Provider = provider,
            EditCommand = new RelayCommand<AiProvider>(value => edited = value),
            ToggleCommand = new RelayCommand<ProviderModelGroupViewModel>(value => value!.IsExpanded = !value.IsExpanded),
            ToggleCommandParameter = group
        };
        var window = new AppWindow { Content = header };
        window.Show();
        try
        {
            var edit = header.FindControl<AppButton>("EditProviderButton")!;
            var toggle = header.FindControl<AppIconButton>("ToggleModelsButton")!;
            PressSpace(edit);
            Assert.Same(provider, edited);
            PressSpace(toggle);
            Assert.True(group.IsExpanded);
            header.IsExpanded = group.IsExpanded;
            Assert.True(toggle.IsAlternateIcon);
            Assert.Equal($"收起 {provider.Name} 的模型", toggle.Label);
            header.ModelCount = 4;
            Assert.Equal("模型 4", header.FindControl<AppText>("ModelCountText")!.Text);
            var replacement = ProviderSettingsRowTestData.Provider() with { Name = "replacement", IsDefault = false };
            header.Provider = replacement;
            Assert.Equal("replacement", header.FindControl<AppText>("ProviderNameText")!.Text);
            Assert.Equal("编辑提供商 replacement", AutomationProperties.GetName(edit));
            Assert.False(header.FindControl<AppText>("DefaultBadge")!.IsVisible);
            header.ToggleCommand = null;
            Assert.False(toggle.IsEffectivelyEnabled);
            header.Provider = null;
            Assert.False(edit.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ModelCommands_KeepParametersCurrentAndRespectCanExecute() => _session.Dispatch(() =>
    {
        var model = ProviderSettingsRowTestData.Model(Guid.NewGuid());
        ModelProfile? edited = null;
        ModelProfile? deleted = null;
        var allowed = false;
        var deleteCommand = new RelayCommand<ModelProfile>(value => deleted = value, _ => allowed);
        var row = new AppModelSettingsRow
        {
            Model = model,
            EditCommand = new RelayCommand<ModelProfile>(value => edited = value),
            DeleteCommand = deleteCommand
        };
        var window = new AppWindow { Content = row };
        window.Show();
        try
        {
            var edit = row.FindControl<AppButton>("EditModelButton")!;
            var delete = row.FindControl<AppButton>("DeleteModelButton")!;
            PressSpace(edit);
            Assert.Same(model, edited);
            Assert.False(delete.IsEffectivelyEnabled);
            allowed = true;
            deleteCommand.NotifyCanExecuteChanged();
            var replacement = model with { Id = Guid.NewGuid(), DisplayName = "replacement", ContextLength = 256000, ReasoningEffort = "high" };
            row.Model = replacement;
            Assert.Equal("上下文 256000", row.FindControl<AppText>("ContextText")!.Text);
            Assert.Equal("思考 high", row.FindControl<AppText>("ReasoningText")!.Text);
            Assert.Equal("编辑模型 replacement", AutomationProperties.GetName(edit));
            Assert.Equal("删除模型 replacement", AutomationProperties.GetName(delete));
            PressSpace(delete);
            Assert.Same(replacement, deleted);
            row.DeleteCommand = null;
            Assert.False(delete.IsEffectivelyEnabled);
            row.Model = null;
            Assert.False(edit.IsEffectivelyEnabled);
            Assert.Null(edit.CommandParameter);
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
