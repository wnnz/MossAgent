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
public sealed class InspectorArtifactEmptyLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, 640, false)]
    [InlineData(1280, 820, false)]
    [InlineData(1920, 1032, false)]
    [InlineData(980, 640, true)]
    [InlineData(1280, 820, true)]
    [InlineData(1920, 1032, true)]
    public Task ArtifactEmptyState_KeepsEveryLineInsideItsFixedHeightRow(int width, int height, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "合成审计对话",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        fixture.Main.SelectInspectorTabCommand.Execute("audit");
        await fixture.Main.Audit.RefreshCommand.ExecuteAsync(null);
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = width,
            Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var empty = Assert.Single(window.GetVisualDescendants().OfType<AppEmptyState>(), control => control.Title == InspectorText.NoArtifacts);
            Assert.True(empty.IsEffectivelyVisible);
            var row = Assert.IsType<Grid>(empty.Parent);
            var rowTop = row.RowDefinitions.Take(4).Sum(definition => definition.ActualHeight);
            var rowBottom = rowTop + row.RowDefinitions[4].ActualHeight;
            foreach (var text in empty.GetVisualDescendants().OfType<AppText>().Where(text => text.IsEffectivelyVisible))
            {
                var origin = text.TranslatePoint(new Point(), row)!.Value;
                Assert.True(origin.Y >= rowTop, $"{text.Text}: top {origin.Y} is above {rowTop}");
                Assert.True(origin.Y + text.Bounds.Height <= rowBottom + 0.1,
                    $"{text.Text}: bottom {origin.Y + text.Bounds.Height} exceeds {rowBottom}");
            }
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
