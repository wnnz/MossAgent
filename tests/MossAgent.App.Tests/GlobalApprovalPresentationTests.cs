using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GlobalApprovalPresentationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "workspace")]
    [InlineData(true, "workspace")]
    [InlineData(false, "settings")]
    [InlineData(true, "settings")]
    [InlineData(false, "automations")]
    [InlineData(true, "automations")]
    [InlineData(false, "inspector")]
    [InlineData(true, "inspector")]
    public Task PendingRequestIsVisibleOnEveryPage(bool dark, string page) => WithWindow(dark, (window, fixture, token) =>
    {
        if (page == "settings") fixture.Main.SelectSettingsTabCommand.Execute("5");
        else if (page == "automations") fixture.Main.OpenAutomationsCommand.Execute(null);
        else if (page == "inspector") fixture.Main.SelectInspectorTabCommand.Execute("audit");
        Flush(window);
        var focused = window.FocusManager!.GetFocusedElement();
        var first = Request(fixture.Main.Approval, "first", token);
        var view = ApprovalView(window);
        Flush(window);

        AssertReachable(window, view, fixture.Main.Approval);
        Assert.Same(focused, window.FocusManager.GetFocusedElement());
        Assert.Equal(page == "settings", fixture.Main.IsSettingsOpen);
        Assert.Equal(page == "automations", fixture.Main.Pages.IsAutomationsOpen);
        Assert.True(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);
        Assert.False(first.IsCompleted);
        Assert.Empty(fixture.Server.Requests);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PageSwitchResizeAndCancellationRetainRequestIdentityAndDraft(bool dark) => WithWindow(dark, async (window, fixture, token) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var settings = window.FindControl<SettingsOverlay>("SettingsView")!;
        var editor = Assert.Single(settings.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("settings-search"));
        editor.Text = "外观";
        Assert.True(editor.Focus());
        Flush(window);
        using var firstCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var first = Request(fixture.Main.Approval, "first", firstCancellation.Token);
            var firstId = fixture.Main.Approval.CurrentRequestId;
            var second = Request(fixture.Main.Approval, "second", token);
            var view = ApprovalView(window);
            Flush(window);
            AssertReachable(window, view, fixture.Main.Approval);
            Assert.Equal(2, fixture.Main.Approval.PendingCount);
            Assert.Same(editor, window.FocusManager!.GetFocusedElement());

            fixture.Main.CloseSettingsCommand.Execute(null);
            fixture.Main.OpenAutomationsCommand.Execute(null);
            Flush(window);
            AssertReachable(window, view, fixture.Main.Approval);
            fixture.Main.SelectSettingsTabCommand.Execute("5");
            window.Width = 526;
            window.Height = 601;
            Flush(window);
            AssertReachable(window, view, fixture.Main.Approval);
            Assert.Same(settings, window.FindControl<SettingsOverlay>("SettingsView"));
            Assert.Equal("外观", editor.Text);
            Assert.Equal(firstId, fixture.Main.Approval.CurrentRequestId);

            firstCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Flush(window);
            AssertReachable(window, view, fixture.Main.Approval);
            Assert.Equal(1, fixture.Main.Approval.PendingCount);
            Assert.NotEqual(firstId, fixture.Main.Approval.CurrentRequestId);
            Assert.False(fixture.Main.Approval.ApproveCommand.CanExecute(firstId));
            fixture.Main.Approval.ApproveCommand.Execute(firstId);
            Assert.False(second.IsCompleted);
            Assert.True(fixture.Main.IsSettingsOpen);
            Assert.Equal("外观", editor.Text);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { firstCancellation.Cancel(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TabFromSettingsReachesApprovalWithoutClosingOrSubmittingAnything(bool dark) => WithWindow(dark, (window, fixture, token) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var search = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("settings-search"));
        Assert.True(search.Focus(NavigationMethod.Tab));
        var pending = Request(fixture.Main.Approval, "keyboard", token);
        Flush(window);
        var deny = Decision(ApprovalView(window), fixture.Main.Approval, false);
        for (var index = 0; index < 200 && !ReferenceEquals(deny, window.FocusManager!.GetFocusedElement()); index++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Flush(window);
        }

        Assert.Same(deny, window.FocusManager!.GetFocusedElement());
        Assert.True(fixture.Main.IsSettingsOpen);
        Assert.False(pending.IsCompleted);
        Assert.Empty(fixture.Server.Requests);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShutdownOverlayInterceptsInputAbovePendingApproval(bool dark) => WithWindow(dark, (window, fixture, token) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        var pending = Request(fixture.Main.Approval, "shutdown", token);
        Flush(window);
        var deny = Decision(ApprovalView(window), fixture.Main.Approval, false);
        var position = deny.TranslatePoint(new Point(deny.Bounds.Width / 2, deny.Bounds.Height / 2), window)!.Value;
        fixture.Workspace.Shutdown.Begin();
        try
        {
            Flush(window);
            var shutdown = Assert.Single(window.GetVisualDescendants().OfType<AppShutdownOverlay>());
            Assert.True(shutdown.IsEffectivelyVisible);
            var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(position));
            Assert.True(ReferenceEquals(shutdown, hit) || shutdown.IsVisualAncestorOf(hit),
                $"Hit {hit.GetType().Name}; ancestors {string.Join(',', hit.GetVisualAncestors().Select(ancestor => ancestor.GetType().Name))}; shutdown {shutdown.Bounds}; point {position}");
            window.MouseDown(position, MouseButton.Left);
            window.MouseUp(position, MouseButton.Left);
            Flush(window);
            Assert.False(pending.IsCompleted);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { fixture.Workspace.Shutdown.Complete(); }
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsShortcutRetainsThePendingApprovalWithoutApprovingIt(bool dark) => WithWindow(dark, (window, fixture, token) =>
    {
        var pending = Request(fixture.Main.Approval, "settings-shortcut", token);
        var requestId = fixture.Main.Approval.CurrentRequestId;
        Flush(window);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            window.KeyPress(Key.OemComma, RawInputModifiers.Control, PhysicalKey.Comma, ",");
            window.KeyRelease(Key.OemComma, RawInputModifiers.Control, PhysicalKey.Comma, ",");
            Flush(window);
            Assert.True(fixture.Main.IsSettingsOpen);
            AssertReachable(window, ApprovalView(window), fixture.Main.Approval);
            Assert.Equal(requestId, fixture.Main.Approval.CurrentRequestId);
            Assert.False(pending.IsCompleted);
            Assert.True(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);
            Assert.Empty(fixture.Server.Requests);
        }
        return Task.CompletedTask;
    });

    private Task WithWindow(bool dark, Func<MainWindow, WorkspaceModelSelectionFixture, CancellationToken, Task> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = 1280,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            await action(window, fixture, cancellation.Token);
        }
        finally
        {
            cancellation.Cancel();
            Flush(window);
            Assert.False(fixture.Main.Approval.IsPending);
            Assert.False(ApprovalView(window).IsEffectivelyVisible);
            Assert.False(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    private static Task<bool> Request(ApprovalViewModel approval, string callId, CancellationToken token)
    {
        var descriptor = new ToolDescriptor("filesystem.replace_text", "合成审批呈现请求", "{}",
            ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var request = new ToolRequest(callId, descriptor.Name,
            JsonSerializer.SerializeToElement(new { path = @"C:\fixture\draft.txt", oldText = "before", newText = "after" }));
        return approval.RequestAsync(descriptor, request, token).AsTask();
    }

    private static ToolApprovalView ApprovalView(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<ToolApprovalView>());

    private static AppButton Decision(ToolApprovalView view, ApprovalViewModel approval, bool approve) =>
        Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => ReferenceEquals(button.Command,
            approve ? approval.ApproveCommand : approval.DenyCommand));

    private static void AssertReachable(MainWindow window, ToolApprovalView view, ApprovalViewModel approval)
    {
        Assert.True(view.IsEffectivelyVisible);
        var card = Assert.Single(view.GetVisualDescendants().OfType<AppCard>(), control => control.Classes.Contains("tool-approval-card"));
        foreach (var control in new Control[] { card, Decision(view, approval, false), Decision(view, approval, true) })
        {
            Assert.True(control.IsEffectivelyVisible);
            Assert.True(control.IsEffectivelyEnabled);
            var bounds = new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
            Assert.True(bounds.Width > 0 && bounds.Height > 0);
            Assert.True(new Rect(window.ClientSize).Contains(bounds), bounds.ToString());
        }
        Assert.Equal(approval.CurrentRequestId, Decision(view, approval, false).CommandParameter);
        Assert.Equal(approval.CurrentRequestId, Decision(view, approval, true).CommandParameter);
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
