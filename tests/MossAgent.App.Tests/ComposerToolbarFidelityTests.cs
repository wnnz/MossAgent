using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerToolbarFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    [InlineData(false, 820)]
    [InlineData(true, 820)]
    public Task Toolbar_MatchesReferenceGeometryAndUsesCompactModelName(bool dark, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
        var window = CreateWindow(view, dark, height);
        window.Show();
        try
        {
            window.UpdateLayout();
            AssertToolbarGeometry(view);
            var trigger = ModelTrigger(view);
            Assert.Equal(fixture.SelectedModel.DisplayName, trigger.Label);
            Assert.Equal(fixture.Workspace.ModelPicker.CurrentDisplayName, ToolTip.GetTip(trigger));
            Assert.Equal(fixture.Workspace.ModelPicker.CurrentDisplayName, AutomationProperties.GetName(trigger));
            trigger.Command!.Execute(trigger.CommandParameter);
            Assert.True(fixture.Workspace.ModelPicker.IsQuickMenuVisible);
            Assert.Equal("合成请求，验证明确选择的模型。", fixture.Workspace.ComposerText);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SameDisplayNames_KeepProviderIdentityAndUpdateBoundTrigger(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var duplicate = fixture.AlternativeModel with { DisplayName = fixture.SelectedModel.DisplayName };
        await fixture.Configurations.SaveModelAsync(duplicate, fixture.Token);
        await fixture.Workspace.ModelPicker.ReloadAsync();
        var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
        var window = CreateWindow(view, dark, 640);
        window.Show();
        try
        {
            window.UpdateLayout();
            var trigger = ModelTrigger(view);
            var previousDescription = AutomationProperties.GetName(trigger);
            Assert.True(fixture.Workspace.ModelPicker.TrySelect(duplicate.ProviderId, duplicate.Id));
            window.UpdateLayout();
            Assert.Equal(duplicate.DisplayName, trigger.Label);
            Assert.Equal(duplicate.Id, fixture.Workspace.SelectedModel?.Id);
            Assert.NotEqual(previousDescription, AutomationProperties.GetName(trigger));
            Assert.Equal(fixture.Workspace.ModelPicker.CurrentDisplayName, AutomationProperties.GetName(trigger));
            Assert.Equal(fixture.Workspace.ModelPicker.CurrentDisplayName, ToolTip.GetTip(trigger));
            Assert.Equal("合成请求，验证明确选择的模型。", fixture.Workspace.ComposerText);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisabledSelection_StillShowsUnavailableNoticeAndCannotSend(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
        var window = CreateWindow(view, dark, 640);
        window.Show();
        try
        {
            await fixture.SetSelectedModelEnabledAsync(false);
            window.UpdateLayout();
            var trigger = ModelTrigger(view);
            Assert.True(fixture.Workspace.ModelPicker.IsSelectedUnavailable);
            Assert.Equal(fixture.Workspace.ModelPicker.CurrentDisplayName, trigger.Label);
            Assert.NotEqual("选择模型", trigger.Label);
            Assert.False(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
            Assert.Equal("合成请求，验证明确选择的模型。", fixture.Workspace.ComposerText);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow CreateWindow(WorkspaceComposerView view, bool dark, int height) => new()
    {
        Width = 800,
        Height = height,
        Content = view,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static AppActionButton ModelTrigger(WorkspaceComposerView view) =>
        Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(), button => button.Classes.Contains("composer-model-picker"));

    private static void AssertToolbarGeometry(WorkspaceComposerView view)
    {
        var buttons = view.GetVisualDescendants().OfType<AppIconButton>().ToArray();
        var send = Assert.Single(buttons, button => button.Name == "SendButton");
        var attach = Assert.Single(buttons, button => button.Classes.Contains("composer-attach"));
        Assert.Equal(new Size(28, 28), send.Bounds.Size);
        Assert.Equal(new CornerRadius(14), send.CornerRadius);
        var sendIcon = Assert.Single(send.GetVisualDescendants().OfType<AppIcon>());
        var attachIcon = Assert.Single(attach.GetVisualDescendants().OfType<AppIcon>());
        Assert.Equal(12, sendIcon.Size);
        Assert.Equal(12, attachIcon.Size);
        var sendPosition = sendIcon.TranslatePoint(default, view)!.Value;
        var attachPosition = attachIcon.TranslatePoint(default, view)!.Value;
        Assert.InRange(Math.Abs(sendPosition.Y + sendIcon.Bounds.Height / 2
            - attachPosition.Y - attachIcon.Bounds.Height / 2), 0, 0.5);
    }
}
