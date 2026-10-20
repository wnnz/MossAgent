using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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
public sealed class ChatHeaderInspectorTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, "Light")]
    [InlineData(1280, "Light")]
    [InlineData(1920, "Light")]
    [InlineData(980, "Dark")]
    [InlineData(1280, "Dark")]
    [InlineData(1920, "Dark")]
    public Task HeaderActions_AreAlignedAndUseSharedSizes(double width, string theme) => WithWindow((window, model) =>
    {
        window.Width = width;
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.UpdateLayout();
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        var names = new[] { "ChatMoreButton", "ChatContextButton", "ChatInspectorButton" };
        var header = Assert.Single(chat.GetVisualDescendants().OfType<AppConversationHeader>());
        var buttons = names.Select(name => header.FindControl<AppIconButton>(name)!).ToArray();
        Assert.All(buttons, button => Assert.NotNull(button));
        Assert.All(buttons, button =>
        {
            Assert.Equal(new Size(32, 32), button.Bounds.Size);
            Assert.True(button.IsEffectivelyVisible);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(button.Name == "ChatContextButton" ? 12 : 10, icon.Size);
            Assert.Equal(1, icon.StrokeThickness);
        });
        var bounds = buttons.Select(button => Bounds(button, window)).ToArray();
        Assert.All(bounds, rectangle => Assert.Equal(bounds[0].Center.Y, rectangle.Center.Y));
        Assert.True(bounds[0].Right <= bounds[1].Left);
        Assert.True(bounds[1].Right < bounds[2].Left);
        Assert.True(bounds[2].Right <= window.ClientSize.Width);
        var separator = header.FindControl<AppBorder>("ChatInspectorSeparator")!;
        Assert.Equal(new Size(1, 16), separator.Bounds.Size);
        Assert.False(separator.IsHitTestVisible);
        var indicator = Assert.Single(chat.GetVisualDescendants().OfType<AppBorder>(),
            control => control.Classes.Contains("context-indicator"));
        Assert.Equal(new Size(28, 28), indicator.Bounds.Size);
        Assert.True(model.IsContextCardVisible);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task StartupWarning_KeepsHeadingAndActionsInsideTheNarrowChat(bool dark) => WithWindow((window, model) =>
    {
        window.Width = 980;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        model.Workspace.Startup.Complete(true);
        model.ToggleRightPanelCommand.Execute(null);
        window.UpdateLayout();
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        var header = Assert.Single(chat.GetVisualDescendants().OfType<AppBorder>(),
            control => control.Classes.Contains("chat-header"));
        var bounds = Bounds(header, window);
        var heading = Assert.Single(chat.GetVisualDescendants().OfType<AppIconLabel>(),
            control => control.Classes.Contains("conversation-heading"));
        Assert.True(heading.Bounds.Width >= 100);
        foreach (var name in new[] { "ChatMoreButton", "ChatContextButton" })
        {
            var button = chat.GetVisualDescendants().OfType<AppIconButton>().Single(control => control.Name == name);
            var actionBounds = Bounds(button, window);
            Assert.True(actionBounds.Left >= bounds.Left);
            Assert.True(actionBounds.Right <= bounds.Right);
        }
        Assert.False(chat.GetVisualDescendants().OfType<AppIconButton>()
            .Single(control => control.Name == "ChatInspectorButton").IsEffectivelyVisible);
        var pane = Assert.Single(window.GetVisualDescendants().OfType<InspectorPane>());
        var close = Assert.Single(pane.GetVisualDescendants().OfType<AppIconButton>(),
            control => ReferenceEquals(control.Command, model.ToggleRightPanelCommand));
        Assert.True(close.IsEffectivelyVisible);
        Assert.True(Bounds(close, window).Right <= Bounds(pane, window).Right);
    }, running: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardToggle_UpdatesBothButtonsAndRestoresContextPreference(bool hidden) => WithWindow((window, model) =>
    {
        if (hidden) model.ToggleContextCardCommand.Execute(null);
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        var toggle = Assert.Single(chat.GetVisualDescendants().OfType<AppConversationHeader>())
            .FindControl<AppIconButton>("ChatInspectorButton");
        Assert.NotNull(toggle);
        Assert.Same(model.ToggleRightPanelCommand, toggle.Command);
        Assert.Equal("展开右侧面板", toggle.Label);
        Activate(window, toggle);
        Assert.True(model.IsRightPanelOpen);
        Assert.False(model.IsContextCardVisible);
        Assert.True(Assert.Single(window.GetVisualDescendants().OfType<AppWorkspacePanes>()).IsInspectorOpen);
        Assert.Equal("收起右侧面板", toggle.Label);
        Assert.Equal(toggle.Label, AutomationProperties.GetName(toggle));
        Assert.Equal(toggle.Label, ToolTip.GetTip(toggle));
        Assert.Contains("active", toggle.Classes);
        Assert.True(toggle.IsAlternateIcon);
        Assert.False(toggle.IsEffectivelyVisible);
        var pane = Assert.Single(window.GetVisualDescendants().OfType<InspectorPane>());
        var card = Assert.Single(pane.GetVisualDescendants().OfType<AppCard>(),
            control => control.Classes.Contains("inspector-surface"));
        Assert.False(card.IsEffectivelyVisible);
        var composer = Assert.Single(chat.GetVisualDescendants().OfType<WorkspaceComposerView>());
        Assert.True(Bounds(composer, window).Right <= Bounds(pane, window).Left);
        var close = Assert.Single(pane.GetVisualDescendants().OfType<AppIconButton>(),
            button => ReferenceEquals(button.Command, model.ToggleRightPanelCommand));
        Assert.Equal(toggle.Label, close.Label);
        Activate(window, close);
        Assert.False(model.IsRightPanelOpen);
        Assert.Equal(!hidden, model.IsContextCardVisible);
        Assert.Equal(!hidden, card.IsEffectivelyVisible);
        Assert.Equal("展开右侧面板", toggle.Label);
        Assert.DoesNotContain("active", toggle.Classes);
        Assert.False(toggle.IsAlternateIcon);
        Assert.True(toggle.IsEffectivelyVisible);
    });

    [Fact]
    public Task ContextAction_RestoresFloatingCardFromAnotherInspectorTab() => WithWindow((window, model) =>
    {
        model.SelectInspectorTabCommand.Execute("audit");
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        var header = Assert.Single(chat.GetVisualDescendants().OfType<AppConversationHeader>());
        var context = header.FindControl<AppIconButton>("ChatContextButton");
        Assert.NotNull(context);
        Activate(window, context);
        Assert.False(model.IsRightPanelOpen);
        Assert.True(model.IsContextCardVisible);
        Assert.Equal("audit", model.ActiveInspectorTab);
        Assert.Equal("展开右侧面板", header.FindControl<AppIconButton>("ChatInspectorButton")!.Label);
    });

    [Fact]
    public Task NoSelectedTask_HidesTheWholeActionGroup() => WithWindow((window, model) =>
    {
        model.Workspace.Catalog.StartNewTask();
        window.UpdateLayout();
        var chat = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceChatView>());
        foreach (var name in new[] { "ChatMoreButton", "ChatContextButton", "ChatInspectorButton", "ChatInspectorSeparator" })
        {
            var control = Assert.Single(chat.GetVisualDescendants().OfType<AppConversationHeader>()).FindControl<Control>(name);
            Assert.NotNull(control);
            Assert.False(control.IsEffectivelyVisible);
        }
    });

    [Fact]
    public void HeaderToggle_UsesSharedGeometryAndAppearance()
    {
        var document = XDocument.Load(Path.Combine(AppPath(), "Controls", "AppConversationHeader.axaml"));
        var toggle = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "ChatInspectorButton"));
        Assert.Equal("{StaticResource UiIconPanelAdd}", toggle.Attribute("IconData")?.Value);
        Assert.Equal("{StaticResource UiIconPanelRight}", toggle.Attribute("AlternateIconData")?.Value);
        Assert.Null(toggle.Attribute("Width"));
        Assert.Null(toggle.Attribute("Height"));
        Assert.Null(toggle.Attribute("Background"));
    }

    private Task WithWindow(Action<MainWindow, MainWindowViewModel> action, bool running = false) => _session.DispatchAsync(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-header-tests-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellation = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellation);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var project = new ProjectProfile(Guid.NewGuid(), "Header fixture", root, [root], DateTimeOffset.UtcNow);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "Header conversation", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            await repository.SaveProjectAsync(project, cancellation);
            await repository.SaveTaskAsync(task, cancellation);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            if (running) AddRunningTask(services, project, task);
            model.Workspace.Catalog.UpsertTask(task);
            var window = new MainWindow { DataContext = model, WindowState = WindowState.Normal };
            window.Show();
            try { window.UpdateLayout(); action(window, model); }
            finally { window.Close(); }
        }
        finally { SqliteTestPool.Clear(root); await DeleteRootAsync(root).ConfigureAwait(false); }
    }, TestContext.Current.CancellationToken);

    private static void AddRunningTask(IServiceProvider services, ProjectProfile project, AgentTask task)
    {
        var provider = new AiProvider(Guid.NewGuid(), "Fixture", ProviderProtocol.OpenAiResponses,
            new Uri("http://127.0.0.1:1/v1"), string.Empty, null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "Fixture", 16000, 2000,
            "medium", null, null, false, false, true, true, "{}");
        var assistant = new ChatMessageViewModel("assistant", string.Empty);
        var state = new WorkspaceRunState(task, project, provider, model, [], assistant)
        {
            Activity = "正在执行工具：" + new string('W', 150)
        };
        services.GetRequiredService<WorkspaceRunTracker>().Add(state);
    }

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

    private static void Activate(MainWindow window, AppIconButton button)
    {
        window.UpdateLayout();
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        window.UpdateLayout();
    }

    private static Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src", "MossAgent.App");
    }
}
