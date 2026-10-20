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
public sealed class GitPushInspectorWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task MainWindowInspectorConfirmsExactCommitAndTargetWithoutSendingStagingOrConsumingTheDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await GitIndexAppFixture.CreateAsync();
        var remote = await fixture.CreatePushRemoteAsync();
        var head = (await fixture.GitAsync("rev-parse", "HEAD")).Trim();
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "tracked.txt"), "unsent dirty content\n", fixture.Token);
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "staged.txt"), "unsent staging\n", fixture.Token);
        await fixture.GitAsync("add", "--", "staged.txt");
        var model = fixture.Main;
        await model.Workspace.Catalog.ReloadAsync();
        model.Workspace.Catalog.UpsertTask(fixture.TaskProfile);
        model.Workspace.ComposerText = "推送不消耗聊天草稿";
        model.IsRightPanelOpen = true;
        model.ActiveInspectorTab = "diff";
        await model.Diff.RefreshCommand.ExecuteAsync(null);
        var window = new MainWindow { DataContext = model, WindowState = WindowState.Normal, Width = 1280, Height = 760 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var actions = Assert.Single(window.GetVisualDescendants().OfType<GitPushActionsView>());
            Assert.Same(model.Diff.Push, actions.DataContext);
            var open = actions.FindControl<AppButton>("OpenPushButton")!;
            Activate(open);
            await model.Diff.Push.OpenCommand.ExecutionTask!;
            window.UpdateLayout();
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<GitPushDialog>());
            Assert.Same(model.Diff.Push, dialog.DataContext);
            Assert.True(model.Diff.Push.CanPush);
            dialog.FindControl<AppInput>("PushBranchInput")!.Text = "review/inspector";
            Assert.False(model.Diff.Push.CanPush);
            Activate(dialog.FindControl<AppButton>("RefreshPushButton")!);
            await model.Diff.Push.RefreshPreviewCommand.ExecutionTask!;
            window.UpdateLayout();
            Activate(dialog.FindControl<AppButton>("ConfirmPushButton")!);
            await model.Diff.Push.PushCommand.ExecutionTask!;
            Dispatcher.UIThread.RunJobs();
            Assert.False(model.Diff.Push.IsOpen);
            Assert.True(open.IsFocused);
            Assert.Equal(InspectorText.Get("GitPushSucceeded"), model.Diff.Push.Status);
            Assert.Equal(head, (await fixture.GitAsync("--git-dir=" + remote, "rev-parse", "refs/heads/review/inspector")).Trim());
            Assert.Equal("baseline\n", await fixture.GitAsync("--git-dir=" + remote, "show", "review/inspector:tracked.txt"));
            Assert.DoesNotContain("staged.txt", await fixture.GitAsync("--git-dir=" + remote, "ls-tree", "--name-only", "review/inspector"));
            Assert.Contains("staged.txt", await fixture.GitAsync("diff", "--cached", "--name-only"));
            Assert.Equal("推送不消耗聊天草稿", model.Workspace.ComposerText);
            var audits = await fixture.Audit.GetRecentAsync(fixture.Context.TaskId, 50, fixture.Token);
            Assert.True(Assert.Single(audits, audit => audit.ToolName == "git.push").IsSuccess);
            Assert.Contains(audits, audit => audit.ToolName == "git.push_preview" && audit.IsSuccess == true);
            await fixture.WaitAsync(() => !model.Diff.IsDiffLoading);
            Assert.Contains(model.Diff.Files, file => file.Path == "tracked.txt");
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
