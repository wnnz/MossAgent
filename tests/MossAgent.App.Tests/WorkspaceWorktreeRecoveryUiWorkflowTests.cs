using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
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

    [Fact]
    public Task RetainedCleanupRowShowsRetryRemoveButtonWhileCreationRowHidesIt() =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var workspace = fixture.Workspace;
            var project = workspace.Catalog.SelectedProject!;
            await WorkspaceWorktreeRecoverySeeding.SeedMissingOrphanAsync(fixture, project);
            var cleanupTaskId = await WorkspaceWorktreeRecoverySeeding.SeedRetainedCleanupOrphanAsync(fixture, project);
            await workspace.WorktreeRecovery.RefreshAsync();
            Assert.True(workspace.WorktreeRecovery.IsVisible);
            Assert.Equal(2, workspace.WorktreeRecovery.Items.Count);
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
                var retries = card.GetVisualDescendants().OfType<AppButton>()
                    .Where(candidate => candidate.Name == "RetryRemoveWorktreeIntentButton").ToList();
                var shown = Assert.Single(retries, candidate => candidate.IsEffectivelyVisible);
                var context = Assert.IsAssignableFrom<WorkspaceWorktreeRecoveryItemViewModel>(shown.DataContext);
                Assert.True(context.IsCleanupItem);
                Assert.Equal(cleanupTaskId, context.TaskId);
                Assert.All(retries.Where(candidate => !candidate.IsEffectivelyVisible),
                    candidate => Assert.False(
                        Assert.IsAssignableFrom<WorkspaceWorktreeRecoveryItemViewModel>(candidate.DataContext).IsCleanupItem));
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RemovedCleanupRowShowsCleanupRegistrationButtonOnlyForThatRow() =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var workspace = fixture.Workspace;
            var project = workspace.Catalog.SelectedProject!;
            await WorkspaceWorktreeRecoverySeeding.SeedMissingOrphanAsync(fixture, project);
            var removedTaskId = await WorkspaceWorktreeRecoverySeeding.SeedRemovedCleanupOrphanAsync(fixture, project);
            await WorkspaceWorktreeRecoverySeeding.SeedRetainedCleanupOrphanAsync(fixture, project);
            await workspace.WorktreeRecovery.RefreshAsync();
            Assert.True(workspace.WorktreeRecovery.IsVisible);
            Assert.Equal(3, workspace.WorktreeRecovery.Items.Count);
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
                var buttons = card.GetVisualDescendants().OfType<AppButton>()
                    .Where(candidate => candidate.Name == "CleanupWorktreeRegistrationButton").ToList();
                var shown = Assert.Single(buttons, candidate => candidate.IsEffectivelyVisible);
                var context = Assert.IsAssignableFrom<WorkspaceWorktreeRecoveryItemViewModel>(shown.DataContext);
                Assert.True(context.IsCleanupItem);
                Assert.Equal(removedTaskId, context.TaskId);
                Assert.Equal("清理注册项", context.CleanupRegistrationLabel);
                Assert.All(buttons.Where(candidate => !candidate.IsEffectivelyVisible),
                    candidate => Assert.NotEqual(removedTaskId,
                        Assert.IsAssignableFrom<WorkspaceWorktreeRecoveryItemViewModel>(candidate.DataContext).TaskId));
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
