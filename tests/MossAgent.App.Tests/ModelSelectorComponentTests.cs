using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelSelectorComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ComposerSelector_ContainsLongLabelsAndRetainsReasoningAndChevron(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var models = fixture.Main.Models;
        models.EditProviderModelCommand.Execute(fixture.SelectedModel);
        models.DisplayName = "验收模型：用于检查很长的中文名称和模型提供商组合时的共享省略布局";
        await models.SaveModelCommand.ExecuteAsync(null);
        var workspace = fixture.Workspace;
        var view = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow
        {
            Width = 980,
            Height = 300,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var action = Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(),
                button => button.Classes.Contains("composer-model-picker"));
            Assert.Equal(workspace.ModelPicker.ComposerDisplayName, action.Label);
            Assert.Equal(320, action.MaxWidth);
            Assert.InRange(action.Bounds.Width, 1, 320);
            Assert.Equal(12, action.FontSize);
            var icon = Assert.Single(action.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsVisible);
            Assert.Equal(12, icon.Size);
            var iconPosition = icon.TranslatePoint(default, action)!.Value;
            Assert.InRange(iconPosition.X + icon.Bounds.Width, 1, action.Bounds.Width);
            workspace.ModelPicker.SelectedReasoning = "高";
            window.UpdateLayout();
            Assert.Equal("高", action.BadgeText);
            var badge = Assert.Single(action.GetVisualDescendants().OfType<AppText>(), text => text.Text == "高");
            Assert.Equal(11, badge.FontSize);
            Assert.True(badge.IsEffectivelyVisible);
            var badgePosition = badge.TranslatePoint(default, action)!.Value;
            iconPosition = icon.TranslatePoint(default, action)!.Value;
            var centerDifference = Math.Abs(badgePosition.Y + badge.Bounds.Height / 2
                - iconPosition.Y - icon.Bounds.Height / 2);
            Assert.True(centerDifference <= 0.5,
                $"Badge {badgePosition} {badge.Bounds.Size}; icon {iconPosition} {icon.Bounds.Size}; button {action.Bounds.Size}");
            action.Command!.Execute(action.CommandParameter);
            Assert.True(workspace.ModelPicker.IsQuickMenuVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
