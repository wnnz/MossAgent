using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorFullViewWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980)]
    [InlineData(false, 1280)]
    [InlineData(false, 1920)]
    [InlineData(true, 980)]
    [InlineData(true, 1280)]
    [InlineData(true, 1920)]
    public Task FullViewOpensOwnedInspectorAndRestoresSplitWithoutLosingTheDraft(bool dark, int width) =>
        WithWindow(dark, width, (window, model) =>
        {
            var panes = Assert.Single(window.GetVisualDescendants().OfType<AppWorkspacePanes>());
            var chat = Assert.IsType<WorkspaceChatView>(panes.PrimaryPane);
            var inspector = Assert.IsType<InspectorPane>(panes.InspectorPane);
            var input = Assert.Single(Assert.Single(chat.GetVisualDescendants().OfType<WorkspaceComposerView>())
                .GetVisualDescendants().OfType<AppInput>());
            input.Text = "未发送的合成草稿";
            var header = Assert.Single(chat.GetVisualDescendants().OfType<AppConversationHeader>());
            var menu = Assert.IsType<AppFlyout>(header.FindControl<AppIconButton>("ChatMoreButton")!.Flyout);
            menu.ShowAt(header.FindControl<AppIconButton>("ChatMoreButton")!);
            window.UpdateLayout();
            var expand = header.FindControl<AppActionButton>("ChatExpandInspectorButton")!;
            Assert.Same(panes.ShowInspectorExpandedCommand, expand.Command);
            Activate(window, expand);
            Assert.True(model.IsRightPanelOpen);
            Assert.True(panes.IsInspectorOpen);
            Assert.True(panes.IsInspectorExpanded);
            Assert.False(chat.IsEffectivelyVisible);
            Assert.False(model.IsContextCardVisible);
            Assert.Equal(panes.Bounds.Width, inspector.Bounds.Width, 3);
            var inspectorHeader = Assert.Single(inspector.GetVisualDescendants().OfType<AppInspectorHeader>());
            var restore = inspectorHeader.FindControl<AppIconButton>("ExpansionButton")!;
            Assert.True(restore.IsAlternateIcon);
            Assert.Equal(InspectorText.PreviewRestore, restore.Label);
            Activate(window, restore);
            Assert.True(model.IsRightPanelOpen);
            Assert.False(panes.IsInspectorExpanded);
            Assert.True(chat.IsEffectivelyVisible);
            Assert.InRange(inspector.Bounds.Width, 280, panes.Bounds.Width - 360);
            Assert.False(restore.IsAlternateIcon);
            Assert.Equal(InspectorText.PreviewExpand, restore.Label);
            Activate(window, inspectorHeader.FindControl<AppIconButton>("CloseButton")!);
            Assert.False(model.IsRightPanelOpen);
            Assert.True(model.IsContextCardVisible);
            Assert.Equal("未发送的合成草稿", input.Text);
            Assert.Same(chat, panes.PrimaryPane);
            Assert.Same(inspector, panes.InspectorPane);
            menu.ShowAt(header.FindControl<AppIconButton>("ChatMoreButton")!);
            window.UpdateLayout();
            Assert.True(expand.IsEffectivelyVisible);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FloatingCardAndHeaderUseTheMeasuredSharedRightInsets(bool dark) => WithWindow(dark, 1920, (window, model) =>
    {
        var panes = Assert.Single(window.GetVisualDescendants().OfType<AppWorkspacePanes>());
        var card = Assert.Single(window.GetVisualDescendants().OfType<AppCard>(),
            control => control.Classes.Contains("inspector-surface"));
        var cardRight = card.TranslatePoint(default, panes)!.Value.X + card.Bounds.Width;
        Assert.True(model.IsContextCardVisible);
        Assert.Equal(300, card.Bounds.Width, 3);
        Assert.Equal(8, panes.Bounds.Width - cardRight, 3);
        var header = Assert.Single(window.GetVisualDescendants().OfType<AppConversationHeader>());
        var actions = Assert.Single(header.GetVisualDescendants().OfType<StackPanel>(),
            control => control.Classes.Contains("conversation-header-actions"));
        Assert.Equal(4, actions.Margin.Right);
        var buttons = actions.GetVisualDescendants().OfType<AppIconButton>().Where(button => button.IsEffectivelyVisible).ToArray();
        Assert.Equal(3, buttons.Length);
        Assert.All(buttons, button => Assert.Equal(new Size(32, 32), button.Bounds.Size));
    });

    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("detached")]
    [InlineData("missing-inspector")]
    public Task InvalidOwnersCannotOpenOrExpandTheInspector(string change) => _session.Dispatch(() =>
    {
        var panes = new AppWorkspacePanes { PrimaryPane = new AppInput(), InspectorPane = new AppBorder() };
        var host = new AppBorder { Child = panes };
        var window = new AppWindow { Width = 1000, Height = 500, Content = host };
        Assert.False(panes.ShowInspectorExpandedCommand.CanExecute(null));
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(panes.ShowInspectorExpandedCommand.CanExecute(null));
            switch (change)
            {
                case "hidden": host.IsVisible = false; break;
                case "disabled": host.IsEnabled = false; break;
                case "detached": host.Child = null; break;
                case "missing-inspector": panes.InspectorPane = null; break;
            }
            Assert.False(panes.ShowInspectorExpandedCommand.CanExecute(null));
            panes.ShowInspectorExpandedCommand.Execute(null);
            Assert.False(panes.IsInspectorOpen);
            Assert.False(panes.IsInspectorExpanded);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private Task WithWindow(bool dark, int width, Action<MainWindow, MainWindowViewModel> action) => _session.Dispatch<Task>(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-full-view-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var token = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "Full view fixture", root, [root], now);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "Full view", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, now, now, null);
            await repository.SaveProjectAsync(project, token);
            await repository.SaveTaskAsync(task, token);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            model.Workspace.Catalog.UpsertTask(task);
            var window = new MainWindow
            {
                DataContext = model,
                WindowState = WindowState.Normal,
                Width = width,
                Height = 640,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try { window.UpdateLayout(); action(window, model); }
            finally { window.Close(); }
        }
        finally { SqliteTestPool.Clear(root); await DeleteRootAsync(root); }
    }, TestContext.Current.CancellationToken).Unwrap();

    private static async Task DeleteRootAsync(string root)
    {
        var started = TimeProvider.System.GetTimestamp();
        while (Directory.Exists(root))
        {
            try { Directory.Delete(root, recursive: true); return; }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33
                && TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(3))
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
    }

    private static void Activate(AppWindow window, AppButton button)
    {
        window.UpdateLayout();
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        window.UpdateLayout();
    }
}
