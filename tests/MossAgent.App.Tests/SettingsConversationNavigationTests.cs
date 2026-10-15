using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsConversationNavigationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "shortcut")]
    [InlineData(true, "shortcut")]
    [InlineData(false, "menu")]
    [InlineData(true, "menu")]
    public Task NewTaskEntry_LeavesSettingsWithoutSendingOrClearingTheDraft(bool dark, string entry) => WithWindow(dark, async (window, fixture) =>
    {
        var draft = fixture.Workspace.ComposerText;
        var project = fixture.Workspace.Catalog.SelectedProject;
        if (entry == "shortcut")
        {
            Assert.True(Search(window).Focus(NavigationMethod.Tab));
            window.KeyPress(Key.N, RawInputModifiers.Control, PhysicalKey.N, null);
            window.KeyRelease(Key.N, RawInputModifiers.Control, PhysicalKey.N, null);
        }
        else
        {
            var file = Assert.Single(window.GetVisualDescendants().OfType<AppButton>(), button => Equals(button.Content, "文件"));
            Activate(file);
            var content = Assert.IsType<AppCard>(Assert.IsType<AppFlyout>(file.Flyout).Content);
            Activate(Assert.Single(content.GetVisualDescendants().OfType<AppButton>(), button => Equals(button.Content, "新建任务")));
        }
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        Assert.Same(project, fixture.Workspace.Catalog.SelectedProject);
        Assert.Equal(draft, fixture.Workspace.ComposerText);
        Assert.True(Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>()).IsEffectivelyVisible);
        Assert.False(Assert.Single(window.GetVisualDescendants().OfType<SettingsOverlay>()).IsEffectivelyVisible);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Empty(fixture.Server.Requests);
        Assert.Single(await fixture.Tasks.GetTasksAsync(project!.Id, fixture.Token));
    });

    [Theory]
    [InlineData("current-task")]
    [InlineData("different-task")]
    [InlineData("back")]
    [InlineData("forward")]
    public Task ExplicitConversationRequest_LeavesSettingsForTheRequestedTarget(string entry) => WithWindow(false, async (window, fixture) =>
    {
        var catalog = fixture.Workspace.Catalog;
        var current = catalog.SelectedTask!;
        var other = current with { Id = Guid.NewGuid(), Title = "另一个合成导航对话" };
        await fixture.Tasks.SaveTaskAsync(other, fixture.Token);
        catalog.UpsertTask(other, select: false);
        if (entry is "back" or "forward")
        {
            catalog.NewTaskCommand.Execute(null);
            catalog.SelectTaskCommand.Execute(other);
            if (entry == "forward") catalog.NavigateBackCommand.Execute(null);
            fixture.Main.OpenSettingsCommand.Execute(null);
        }
        if (entry == "back") catalog.NavigateBackCommand.Execute(null);
        else if (entry == "forward") catalog.NavigateForwardCommand.Execute(null);
        else catalog.SelectTaskCommand.Execute(entry == "current-task" ? current : other);
        window.UpdateLayout();
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.False(fixture.Main.Pages.IsAutomationsOpen);
        Assert.Equal(entry == "back" ? (Guid?)null : entry == "current-task" ? current.Id : other.Id, catalog.SelectedTask?.Id);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData("rename")]
    [InlineData("reload")]
    public Task BackgroundCatalogChange_KeepsSettingsAndTheUnsavedProviderDraft(string change) => WithWindow(false, async (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("0");
        fixture.Main.Providers.OpenCreateFormCommand.Execute(null);
        fixture.Main.Providers.Name = "未保存的合成配置";
        var task = fixture.Workspace.Catalog.SelectedTask!;
        if (change == "rename") fixture.Workspace.Catalog.UpsertTask(task with { Title = "后台更新合成标题" });
        else await fixture.Workspace.Catalog.ReloadAsync();
        await fixture.WaitUntilAsync(() => fixture.Workspace.Catalog.SelectedTask is not null);
        Assert.True(fixture.Main.IsSettingsOpen);
        Assert.True(fixture.Main.Providers.IsFormOpen);
        Assert.Equal("未保存的合成配置", fixture.Main.Providers.Name);
        Assert.Equal(0, fixture.Main.SettingsTabIndex);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData("locked-task")]
    [InlineData("locked-new")]
    [InlineData("foreign-task")]
    [InlineData("null-task")]
    [InlineData("disposed")]
    public Task RejectedOrDisposedRequest_DoesNotLeaveSettings(string state) => WithWindow(false, (window, fixture) =>
    {
        var catalog = fixture.Workspace.Catalog;
        var task = catalog.SelectedTask!;
        var draft = fixture.Workspace.ComposerText;
        try
        {
            if (state.StartsWith("locked", StringComparison.Ordinal)) catalog.IsLocked = true;
            if (state == "disposed") fixture.Main.Dispose();
            if (state == "locked-new") catalog.NewTaskCommand.Execute(null);
            else catalog.SelectTaskCommand.Execute(state == "foreign-task" ? task with { ProjectId = Guid.NewGuid() }
                : state == "null-task" ? null : task);
            Assert.True(fixture.Main.IsSettingsOpen);
            Assert.Equal(task.Id, catalog.SelectedTask?.Id);
            Assert.Equal(draft, fixture.Workspace.ComposerText);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { catalog.IsLocked = false; }
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SubmitShortcut_FromSettingsSearchDoesNotSubmitTheHiddenComposer(bool dark) => WithWindow(dark, async (window, fixture) =>
    {
        var task = fixture.Workspace.Catalog.SelectedTask!;
        var draft = fixture.Workspace.ComposerText;
        Assert.True(fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        Assert.True(Search(window).Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(fixture.Main.IsSettingsOpen);
        Assert.False(fixture.Workspace.IsRunning);
        Assert.Equal(draft, fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token));
    });

    private Task WithWindow(bool dark, Func<MainWindow, WorkspaceModelSelectionFixture, Task> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "合成设置导航对话",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        fixture.Workspace.ComposerText = "这条合成草稿不应自动发送。";
        await fixture.WaitUntilAsync(() => fixture.Workspace.PrimaryActionCommand.CanExecute(null));
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = 1280,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); await action(window, fixture); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppInput Search(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<AppInput>(),
        control => control.Classes.Contains("settings-search"));

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
