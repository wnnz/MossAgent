using Avalonia;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorktreeObjectUiWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task LegacyConfirmationThroughKeyboardPreservesDraftAndRequiresManualContinuation(bool dark, bool compact) =>
        _session.DispatchAsync(async () =>
        {
            await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
            var fixture = owner.Fixture;
            var task = await WorktreeObjectRuntimeTests.CreateTaskAsync(owner, legacy: true);
            var denied = await ReadAsync(owner, task, "legacy-before-confirm");
            Assert.Equal("workspace_denied", denied.ErrorCode);
            var workspace = fixture.Workspace;
            workspace.Catalog.UpsertTask(task, select: false);
            workspace.Catalog.SelectedTask = task;
            await fixture.WaitUntilAsync(() => workspace.Messages.Count == 1 && workspace.Catalog.WorktreeAuthorization.ConfirmCommand.CanExecute(null));
            var window = new MainWindow { DataContext = fixture.Main, Width = compact ? 526 : 1280,
                Height = compact ? 601 : 820, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                ProviderUiWorkflowInput.Flush(window);
                var composer = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("composer-input"));
                ProviderUiWorkflowInput.Type(window, composer, "确认时保留的前台草稿");
                var confirm = Assert.Single(window.GetVisualDescendants().OfType<AppButton>(), button => button.Name == "ConfirmWorktreeObjectButton");
                var path = Assert.Single(window.GetVisualDescendants().OfType<AppText>(), text => text.Text == task.WorktreePath);
                Assert.True(path.IsEffectivelyVisible);
                confirm.BringIntoView();
                ProviderUiWorkflowInput.Flush(window);
                var point = confirm.TranslatePoint(new Point(confirm.Bounds.Width / 2, confirm.Bounds.Height / 2), window)!.Value;
                Assert.True(new Rect(window.ClientSize).Contains(point));
                var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
                Assert.True(ReferenceEquals(confirm, hit) || confirm.IsVisualAncestorOf(hit));
                SubagentApprovalInteraction.Save(window, $"worktree-object-{dark}-{compact}-before-confirm");
                ProviderUiWorkflowInput.Press(window, confirm);
                await workspace.Catalog.WorktreeAuthorization.ConfirmCommand.ExecutionTask!.WaitAsync(fixture.Token);
                ProviderUiWorkflowInput.Flush(window);
                Assert.False(confirm.IsEffectivelyEnabled);
                Assert.Equal("确认时保留的前台草稿", workspace.ComposerText);
                Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
                Assert.Equal(AgentTaskStatus.Interrupted, workspace.Catalog.SelectedTask?.Status);
                Assert.Empty(fixture.Server.Requests);
                var grants = Assert.IsAssignableFrom<IGitWorktreeAuthorizationService>(fixture.Worktrees);
                Assert.NotNull(await grants.ReadAuthorizationAsync(task.Id, fixture.Token));
                var read = await ReadAsync(owner, task, "legacy-after-confirm");
                Assert.True(read.IsSuccess, read.Summary);
                Assert.Contains("committed baseline", read.Content);
                SubagentApprovalInteraction.Save(window, $"worktree-object-{dark}-{compact}-confirmed-no-replay");
                ProviderUiWorkflowInput.Press(window, ProviderUiWorkflowInput.SendButton(window));
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
                Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
                Assert.Equal(AgentTaskStatus.Completed, workspace.Catalog.SelectedTask?.Status);
                Assert.Equal(2, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count(item => item.Role == MessageRole.User));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static Task<ToolResult> ReadAsync(WorkspaceWorktreeGitFixture owner, AgentTask task, string callId) =>
        owner.Fixture.Tools.ExecuteAsync(new(callId, "filesystem.read_file", JsonSerializer.SerializeToElement(new { path = "README.md" })),
            owner.Fixture.Panels.CreateContext(task, owner.Project), owner.Fixture.Token);
}
