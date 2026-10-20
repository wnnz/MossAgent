using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerPreparationKeyboardTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Control)]
    public Task PendingSaveBlocksFurtherSubmissionWithoutChangingNewDraft(RawInputModifiers modifiers) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => release.Task };
            using var runs = new WorkspaceRunTracker();
            using var workspace = fixture.CreateWorkspace(repository, runs);
            await workspace.LoadAsync();
            workspace.ComposerText = "第一条保存中的消息。";
            var window = new AppWindow
            {
                Width = 900,
                Height = 700,
                Content = new WorkspaceChatView { DataContext = workspace }
            };
            window.Show();
            try
            {
                Flush(window);
                var input = Assert.Single(window.GetVisualDescendants().OfType<AppComposerInput>());
                Assert.True(input.Focus());
                Press(window, modifiers);
                Assert.True(workspace.IsRunning);
                Assert.False(workspace.HasActiveRun);
                Assert.False(input.IsSubmissionEnabled);
                Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
                Assert.Equal("取消发送准备", workspace.PrimaryActionLabel);
                workspace.ComposerText = "下一条尚未发送的草稿。";
                Flush(window);
                Press(window, modifiers);
                Assert.Equal("下一条尚未发送的草稿。", workspace.ComposerText);
                Assert.True(workspace.IsRunning);
                Assert.Equal("取消发送准备", workspace.PrimaryActionLabel);
                Assert.Empty(fixture.Server.Requests);
                release.TrySetResult();
                await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
                Assert.Equal("下一条尚未发送的草稿。", workspace.ComposerText);
                Assert.True(input.IsSubmissionEnabled);
                Assert.Single(fixture.Server.Requests);
            }
            finally
            {
                release.TrySetResult();
                await fixture.WaitUntilAsync(() => !workspace.IsRunning);
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private static void Press(AppWindow window, RawInputModifiers modifiers)
    {
        window.KeyPress(Key.Enter, modifiers, PhysicalKey.Enter, "\r");
        window.KeyRelease(Key.Enter, modifiers, PhysicalKey.Enter, "\r");
        Flush(window);
    }

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
