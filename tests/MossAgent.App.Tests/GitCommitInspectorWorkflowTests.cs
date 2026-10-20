using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitCommitInspectorWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task MainWindowInspectorConfirmsARealStagedCommitWithoutConsumingTheChatDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await GitIndexAppFixture.CreateAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "selected.txt"), "staged content\n", fixture.Token);
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "unselected.txt"), "preserved\n", fixture.Token);
        await fixture.GitAsync("add", "--", "selected.txt");
        var head = await fixture.GitAsync("rev-parse", "HEAD");
        var model = fixture.Main;
        await model.Workspace.Catalog.ReloadAsync();
        model.Workspace.Catalog.UpsertTask(fixture.TaskProfile);
        model.Workspace.ComposerText = "未发送的对话草稿";
        model.IsRightPanelOpen = true;
        model.ActiveInspectorTab = "diff";
        Assert.True(model.Diff.HasTask);
        await model.Diff.RefreshCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model, WindowState = WindowState.Normal, Width = 1280, Height = 760 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var actions = Assert.Single(window.GetVisualDescendants().OfType<GitCommitActionsView>());
            Assert.Same(model.Diff.Commit, actions.DataContext);
            var open = actions.FindControl<AppButton>("OpenCommitButton")!;
            Activate(open);
            await model.Diff.Commit.OpenCommand.ExecutionTask!;
            window.UpdateLayout();
            Assert.True(model.Diff.Commit.IsOpen);
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<GitCommitDialog>());
            Assert.Same(model.Diff.Commit, dialog.DataContext);
            dialog.FindControl<AppInput>("CommitMessageInput")!.Text = "测试实际侧栏提交";
            window.UpdateLayout();
            Activate(dialog.FindControl<AppButton>("ConfirmCommitButton")!);
            await model.Diff.Commit.CommitCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            Assert.False(model.Diff.Commit.IsOpen);
            Assert.True(open.IsFocused);
            Assert.Equal(InspectorText.Get("GitCommitSucceeded"), model.Diff.Commit.Status);
            Assert.NotEqual(head, await fixture.GitAsync("rev-parse", "HEAD"));
            Assert.Equal("staged content\n", await fixture.GitAsync("show", "HEAD:selected.txt"));
            Assert.DoesNotContain("unselected.txt", await fixture.GitAsync("ls-tree", "--name-only", "HEAD"));
            Assert.Equal("未发送的对话草稿", model.Workspace.ComposerText);
            var audits = await fixture.Audit.GetRecentAsync(fixture.Context.TaskId, 50, fixture.Token);
            Assert.True(Assert.Single(audits, audit => audit.ToolName == "git.commit").IsSuccess);
            await fixture.WaitAsync(() => !model.Diff.IsDiffLoading);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Activate(AppButton button)
    {
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
