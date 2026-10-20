using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ConversationHeadingLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, false)]
    [InlineData(1280, false)]
    [InlineData(1920, false)]
    [InlineData(980, true)]
    [InlineData(1280, true)]
    [InlineData(1920, true)]
    public Task SingleConversation_UsesAnAlignedBorderlessHeading(double width, bool dark) => WithWindow((window, model, second) =>
    {
        window.Width = width;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.UpdateLayout();
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        var view = Assert.Single(chat.GetVisualDescendants().OfType<WorkspaceConversationTabsView>());
        var heading = view.FindControl<AppIconLabel>("ConversationHeading");
        Assert.NotNull(heading);
        Assert.True(heading.IsEffectivelyVisible);
        Assert.Equal(model.Workspace.Tabs.SelectedTab!.Title, heading.Label);
        Assert.Equal(32, heading.Bounds.Height);
        Assert.False(heading.IsHitTestVisible);
        Assert.False(Assert.Single(view.GetVisualDescendants().OfType<AppSelectionScrollViewer>()).IsEffectivelyVisible);
        var header = Assert.Single(chat.GetVisualDescendants().OfType<AppBorder>(), control => control.Classes.Contains("chat-header"));
        Assert.Equal(default, header.BorderThickness);
        Assert.Equal(20, header.Padding.Left);
        Assert.Equal(16, heading.Spacing);
        Assert.Equal(TextRenderingMode.Antialias, TextOptions.GetTextRenderingMode(window));
        Assert.True(heading.TryFindResource("UiIconFolder", out var folder));
        Assert.Same(folder, heading.IconData);
        Assert.Equal(16, Assert.IsAssignableFrom<Geometry>(folder).Bounds.Height);
        var icon = Assert.Single(heading.GetVisualDescendants().OfType<AppIcon>());
        var label = Assert.Single(heading.GetVisualDescendants().OfType<AppText>());
        Assert.Equal(12, icon.Size);
        Assert.Equal(1, icon.StrokeThickness);
        Assert.Equal(14, label.FontSize);
        Assert.Equal(FontWeight.Medium, label.FontWeight);
        Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
        var iconBounds = Bounds(icon, window);
        var labelBounds = Bounds(label, window);
        Assert.Equal(Bounds(chat, window).Left + 20, iconBounds.Left, 3);
        Assert.Equal(iconBounds.Right + 16, labelBounds.Left, 3);
        Assert.InRange(Math.Abs(iconBounds.Center.Y - labelBounds.Center.Y), 0, 0.5);
        Assert.Equal(70, iconBounds.Center.Y, 3);
        var actions = Assert.Single(chat.GetVisualDescendants().OfType<AppConversationHeader>())
            .FindControl<AppIconButton>("ChatMoreButton")!;
        Assert.True(Bounds(heading, window).Right <= Bounds(actions, window).Left);
        Assert.True(labelBounds.Right <= Bounds(heading, window).Right);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ClosingSelectedOrBackgroundTab_RestoresTheSingleHeading(bool closeSelected) => WithWindow((window, model, second) =>
    {
        var tabs = model.Workspace.Tabs;
        var first = tabs.SelectedTab!;
        model.Workspace.Catalog.SelectTaskCommand.Execute(second);
        window.UpdateLayout();
        var view = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceConversationTabsView>());
        var heading = view.FindControl<AppIconLabel>("ConversationHeading");
        Assert.NotNull(heading);
        var strip = Assert.Single(view.GetVisualDescendants().OfType<AppSelectionScrollViewer>());
        Assert.False(heading.IsEffectivelyVisible);
        Assert.True(strip.IsEffectivelyVisible);
        Assert.Equal(2, tabs.Items.Count);
        tabs.CloseTabCommand.Execute(closeSelected ? tabs.SelectedTab : first);
        window.UpdateLayout();
        Assert.True(heading.IsEffectivelyVisible);
        Assert.False(strip.IsEffectivelyVisible);
        Assert.Equal(closeSelected ? first.Title : second.Title, heading.Label);
        Assert.Single(tabs.Items);
        Assert.NotNull(model.Workspace.Catalog.ProjectNavigation.Single().FindTask(first.Task.Id));
    });

    [Fact]
    public Task NewDraft_PreservesThePreviousTabWithoutShowingAConversationHeader() => WithWindow((window, model, second) =>
    {
        var previous = model.Workspace.Tabs.SelectedTab;
        model.Workspace.Catalog.NewTaskCommand.Execute(null);
        window.UpdateLayout();
        var view = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceConversationTabsView>());
        var heading = view.FindControl<AppIconLabel>("ConversationHeading");
        Assert.NotNull(heading);
        Assert.False(heading.IsEffectivelyVisible);
        Assert.False(Assert.Single(view.GetVisualDescendants().OfType<AppSelectionScrollViewer>()).IsEffectivelyVisible);
        Assert.Same(previous, Assert.Single(model.Workspace.Tabs.Items));
        model.Workspace.Tabs.SelectTabCommand.Execute(previous);
        window.UpdateLayout();
        Assert.True(heading.IsEffectivelyVisible);
        Assert.Equal(previous!.Title, heading.Label);
    });

    [Fact]
    public Task MoreMenu_ClosesTheSingleTabWithoutArchiving() => WithWindow((window, model, second) =>
    {
        var tabs = model.Workspace.Tabs;
        var selected = tabs.SelectedTab!;
        var more = window.GetVisualDescendants().OfType<AppIconButton>().Single(button => button.Name == "ChatMoreButton");
        var flyout = Assert.IsType<AppFlyout>(more.Flyout);
        flyout.ShowAt(more);
        window.UpdateLayout();
        var menu = Assert.Single(Assert.IsType<AppCard>(flyout.Content).GetVisualDescendants().OfType<AppContentControl>(),
            control => control.Classes.Contains("conversation-header-menu"));
        var header = Assert.Single(window.GetVisualDescendants().OfType<AppConversationHeader>());
        Assert.Same(header.MenuContent, menu.Content);
        var close = Assert.IsType<StackPanel>(menu.Content).Children
            .OfType<AppButton>().SingleOrDefault(button => ReferenceEquals(button.Command, tabs.CloseTabCommand));
        Assert.NotNull(close);
        Assert.Equal(tabs.CloseTabLabel, close.Content);
        Assert.Same(selected, close.CommandParameter);
        model.Workspace.Catalog.IsLocked = true;
        Assert.False(close.IsEffectivelyEnabled);
        tabs.CloseTabCommand.Execute(selected);
        Assert.Same(selected, tabs.SelectedTab);
        model.Workspace.Catalog.IsLocked = false;
        Assert.True(close.Focus());
        close.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        close.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        window.UpdateLayout();
        Assert.Empty(tabs.Items);
        Assert.Null(model.Workspace.Catalog.SelectedTask);
        Assert.NotNull(model.Workspace.Catalog.ProjectNavigation.Single().FindTask(selected.Task.Id));
    });

    [Theory]
    [InlineData(980, 640, false, false, false)]
    [InlineData(980, 640, true, true, true)]
    [InlineData(1280, 820, false, true, false)]
    [InlineData(1280, 820, true, false, true)]
    [InlineData(1920, 1032, false, false, true)]
    [InlineData(1920, 1032, true, true, false)]
    public Task WorkspaceFrame_PreservesColumnsAndRoundsAllCorners(int width, int height, bool dark,
        bool collapseSidebar, bool inspector) => WithWindow((window, model, second) =>
    {
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.UpdateLayout();
        var frame = window.FindControl<AppBorder>("WorkspaceFrame")!;
        Assert.Equal(new CornerRadius(12), frame.CornerRadius);
        Assert.False(frame.ClipToBounds);
        var clip = Assert.IsType<AppBorder>(frame.Child);
        Assert.True(clip.ClipToBounds);
        Assert.Equal(frame.CornerRadius, clip.CornerRadius);
        Assert.Equal(Bounds(frame, window), Bounds(clip, window));
        Assert.Equal(new Thickness(0, 0, 4, 4), frame.Margin);
        Assert.Equal(new Rect(52, 44, width - 56, height - 48), Bounds(frame, window));
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        Assert.Equal(collapseSidebar ? 52 : 316, Bounds(chat, window).Left);
        var availableWidth = width - 4 - Bounds(chat, window).Left;
        var inspectorWidth = inspector ? Math.Clamp(availableWidth * 0.47, 280, availableWidth - 364) : 0;
        var expectedRight = width - 4 - inspectorWidth - (inspector ? 4 : 0);
        Assert.InRange(Math.Abs(expectedRight - Bounds(chat, window).Right), 0, 1);
        Assert.Equal(44, Bounds(chat, window).Top);
        Assert.Equal(height - 4, Bounds(chat, window).Bottom);
    }, width, height, collapseSidebar, inspector);

    [Fact]
    public async Task CleanupRetry_CompletesAfterTheHeadlessCallbackReturns()
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-heading-cleanup-tests-").FullName;
        try
        {
            using var locked = new FileStream(Path.Combine(root, "locked.txt"), FileMode.Create,
                FileAccess.ReadWrite, FileShare.None);
            var cleanup = await _session.Dispatch<Task>(() => DeleteRootAsync(root), TestContext.Current.CancellationToken);
            Assert.False(cleanup.IsCompleted);
            locked.Dispose();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private Task WithWindow(Action<MainWindow, MainWindowViewModel, AgentTask> action,
        int width = 1280, int height = 820, bool collapseSidebar = false, bool inspector = false) => _session.DispatchAsync(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-heading-tests-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellation = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellation);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var project = new ProjectProfile(Guid.NewGuid(), "Heading fixture", root, [root], DateTimeOffset.UtcNow);
            var first = new AgentTask(Guid.NewGuid(), project.Id, string.Concat(Enumerable.Repeat("很长的会话标题", 30)),
                ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            var second = first with
            {
                Id = Guid.NewGuid(),
                Title = "Second conversation",
                CreatedAt = first.CreatedAt.AddMinutes(-1),
                UpdatedAt = first.UpdatedAt.AddMinutes(-1)
            };
            await repository.SaveProjectAsync(project, cancellation);
            await repository.SaveTaskAsync(first, cancellation);
            await repository.SaveTaskAsync(second, cancellation);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            model.Workspace.Catalog.SelectTaskCommand.Execute(first);
            if (collapseSidebar) model.ToggleLeftPanelCommand.Execute(null);
            if (inspector) model.ToggleRightPanelCommand.Execute(null);
            var window = new MainWindow { DataContext = model, WindowState = WindowState.Normal, Width = width, Height = height };
            window.Show();
            try { window.UpdateLayout(); action(window, model, second); }
            finally { window.Close(); }
        }
        finally { SqliteTestPool.Clear(root); await DeleteRootAsync(root).ConfigureAwait(false); }
    }, TestContext.Current.CancellationToken);

    private static Rect Bounds(Control control, Control root) => new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    private static async Task DeleteRootAsync(string root)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Directory.Exists(root))
        {
            try { Directory.Delete(root, recursive: true); return; }
            catch (IOException) when (!cleanup.IsCancellationRequested)
            {
                await Task.Delay(30, cleanup.Token).ConfigureAwait(false);
            }
        }
    }
}
