using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorEmptyStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EmptyState_IsCenteredAndDoesNotRepeatItsHeading(bool dark) => _session.Dispatch(() =>
    {
        var empty = new AppEmptyState { Title = "没有内容", Description = "没有内容" };
        var window = new AppWindow
        {
            Width = 320,
            Height = 400,
            Content = empty,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.False(empty.HasDescription);
            Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, empty.VerticalAlignment);
            Assert.Equal(new Thickness(24), empty.Margin);
            Assert.InRange(empty.Bounds.Center.Y, 199, 201);
            empty.Description = "运行工具后会显示内容。";
            window.UpdateLayout();
            Assert.True(empty.HasDescription);
            Assert.InRange(empty.Bounds.Center.Y, 199, 201);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AttachedPaneWithoutBrowser_ShowsEmptyStateInsteadOfAnUnusableToolbar(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "合成对话", ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = 980,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.NotNull(fixture.Main.EmbeddedBrowsers.CurrentPane);
            Assert.False(fixture.Main.EmbeddedBrowsers.CurrentPane.HasBrowser);
            var empty = Assert.Single(window.GetVisualDescendants().OfType<AppEmptyState>(), control => control.Title == InspectorText.NoBrowser);
            Assert.True(empty.IsEffectivelyVisible);
            var title = Assert.Single(empty.GetVisualDescendants().OfType<AppText>(), text => text.Text == InspectorText.NoBrowser);
            var center = title.TranslatePoint(new Point(title.Bounds.Width / 2, title.Bounds.Height / 2), window)!.Value;
            Assert.InRange(center.Y, 400, 550);
            Assert.All(window.GetVisualDescendants().OfType<EmbeddedBrowserPane>(), pane => Assert.False(pane.IsEffectivelyVisible));
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
