using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceComposerPreferenceRoutingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static void Press(AppWindow window, Key key, RawInputModifiers modifiers)
    {
        window.KeyPress(key, modifiers, PhysicalKey.Enter, "\r");
        window.KeyRelease(key, modifiers, PhysicalKey.Enter, "\r");
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task PersistedSteerSwapsEnterAndCtrlEnterRouting() => _session.DispatchAsync(async () =>
    {
        var store = new ComposerPreferenceStoreProbe { Current = ComposerRunningAction.Steer };
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true,
            configure: services => services.AddSingleton<IComposerPreferenceStore>(store));
        var workspace = fixture.Workspace;
        await workspace.ComposerPreferences.LoadAsync();
        var view = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow { Width = 800, Height = 640, Content = view };
        window.Show();
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            workspace.ComposerText = "运行中引导草稿";
            Assert.Equal(ComposerRunningAction.Steer, workspace.ComposerPreferences.Applied);
            var input = Assert.Single(view.GetVisualDescendants().OfType<AppComposerInput>());
            Assert.True(input.Focus(NavigationMethod.Tab));
            Press(window, Key.Enter, RawInputModifiers.None);
            await fixture.WaitUntilAsync(() => workspace.Steering.History.Items.Count == 1 && !workspace.Steering.IsBusy);
            Assert.Single(fixture.Server.Requests);
            Assert.Empty(workspace.Queue.Items);
            workspace.ComposerText = "队列中的下一轮草稿";
            Assert.True(input.Focus(NavigationMethod.Tab));
            Press(window, Key.Enter, RawInputModifiers.Control);
            await fixture.WaitUntilAsync(() => workspace.Queue.Items.Count == 1);
            Assert.Empty(workspace.Steering.History.Items.Skip(1));
            Assert.Single(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DefaultQueueKeepsEnterQueueAndCtrlEnterSteer() => _session.DispatchAsync(async () =>
    {
        var store = new ComposerPreferenceStoreProbe { Current = ComposerRunningAction.Queue };
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true,
            configure: services => services.AddSingleton<IComposerPreferenceStore>(store));
        var workspace = fixture.Workspace;
        await workspace.ComposerPreferences.LoadAsync();
        var view = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow { Width = 800, Height = 640, Content = view };
        window.Show();
        try
        {
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            workspace.ComposerText = "排队的下一轮草稿";
            Assert.Equal(ComposerRunningAction.Queue, workspace.ComposerPreferences.Applied);
            var input = Assert.Single(view.GetVisualDescendants().OfType<AppComposerInput>());
            Assert.True(input.Focus(NavigationMethod.Tab));
            Press(window, Key.Enter, RawInputModifiers.None);
            await fixture.WaitUntilAsync(() => workspace.Queue.Items.Count == 1);
            Assert.Empty(workspace.Steering.History.Items);
            Assert.Single(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
