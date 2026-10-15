using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectlessChatComposerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 1280, false)]
    [InlineData(true, 526, false)]
    [InlineData(false, 1280, true)]
    [InlineData(true, 526, true)]
    public Task ProjectlessComposer_CanSwitchProjectsAndSubmitWithKeyboard(bool dark, int width, bool withProject) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject);
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = width,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            if (withProject) SelectPlainChat(window, fixture);
            fixture.Workspace.ComposerText = "projectless keyboard submission";
            Flush(window);
            var bar = Assert.Single(window.GetVisualDescendants().OfType<AppComposerContextBar>(), item => item.IsEffectivelyVisible);
            Assert.Equal("无项目聊天", bar.ProjectLabel);
            Assert.True(bar.IsProjectAvailable);
            var send = Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(), item => item.Classes.Contains("composer-send"));
            Assert.True(send.IsEffectivelyEnabled);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<AppIconButton>(),
                item => item.Classes.Contains("composer-attach") && item.IsEffectivelyVisible);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<AppApprovalPolicyButton>(), item => item.IsEffectivelyVisible);
            var input = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(), item => item.Classes.Contains("composer-input"));
            Assert.True(input.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            Flush(window);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !fixture.Workspace.IsRunning);
            Assert.Null(fixture.Workspace.Catalog.SelectedTask!.ProjectId);
            Assert.Equal(2, fixture.Workspace.Messages.Count);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void SelectPlainChat(MainWindow window, WorkspaceModelSelectionFixture fixture)
    {
        var bar = Assert.Single(window.GetVisualDescendants().OfType<AppComposerContextBar>(), item => item.IsEffectivelyVisible);
        var button = bar.FindControl<AppActionButton>("ProjectButton")!;
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        flyout.ShowAt(button);
        Flush(window);
        var content = Assert.IsType<AppContentControl>(flyout.Content);
        var action = Assert.Single(content.GetVisualDescendants().OfType<AppActionButton>(),
            item => ReferenceEquals(item.Command, fixture.Workspace.Catalog.NewChatCommand));
        Assert.True(action.IsEffectivelyEnabled);
        action.Command!.Execute(action.CommandParameter);
        Flush(window);
        Assert.Null(fixture.Workspace.Catalog.SelectedProject);
        Assert.False(flyout.IsOpen);
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
