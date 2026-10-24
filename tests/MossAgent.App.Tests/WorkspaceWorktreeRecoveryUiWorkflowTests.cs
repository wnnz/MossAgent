using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceWorktreeRecoveryUiWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task CompactOrphanRowAcknowledgeIsHitTestableAndHidesPanel() =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var workspace = fixture.Workspace;
            var project = workspace.Catalog.SelectedProject!;
            await WorkspaceWorktreeRecoverySeeding.SeedMissingOrphanAsync(fixture, project);
            await workspace.WorktreeRecovery.RefreshAsync();
            Assert.True(workspace.WorktreeRecovery.IsVisible);
            var window = new MainWindow
            {
                DataContext = fixture.Main, Width = 526, Height = 601,
            };
            window.Show();
            try
            {
                ProviderUiWorkflowInput.Flush(window);
                var card = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceWorktreeRecoveryView>());
                Assert.True(card.IsEffectivelyVisible);
                var button = Assert.Single(card.GetVisualDescendants().OfType<AppButton>(),
                    candidate => candidate.Name == "AcknowledgeWorktreeIntentButton");
                Assert.True(button.IsEffectivelyVisible);
                button.BringIntoView();
                ProviderUiWorkflowInput.Flush(window);
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                Assert.True(new Rect(window.ClientSize).Contains(point));
                var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
                Assert.True(ReferenceEquals(button, hit) || button.IsVisualAncestorOf(hit));
                var row = Assert.Single(workspace.WorktreeRecovery.Items);
                ProviderUiWorkflowInput.Press(window, button);
                await row.AcknowledgeCommand.ExecutionTask!.WaitAsync(fixture.Token);
                ProviderUiWorkflowInput.Flush(window);
                Assert.False(workspace.WorktreeRecovery.IsVisible);
                Assert.DoesNotContain(card.GetVisualDescendants().OfType<AppButton>(),
                    candidate => candidate.Name == "AcknowledgeWorktreeIntentButton" && candidate.IsEffectivelyVisible);
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
